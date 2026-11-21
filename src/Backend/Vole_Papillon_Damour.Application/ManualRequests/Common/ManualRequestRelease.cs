using ErrorOr;
using Microsoft.EntityFrameworkCore;
using Vole_Papillon_Damour.Application.Books.Common;
using Vole_Papillon_Damour.Application.Common.Interfaces.Persistence;
using Vole_Papillon_Damour.Domain.AssoEventsAggregate;
using Vole_Papillon_Damour.Domain.BookAggregate;
using Vole_Papillon_Damour.Domain.BookAggregate.Entities;
using Vole_Papillon_Damour.Domain.BookAggregate.ValueObjects;
using Vole_Papillon_Damour.Domain.BookMovementAggregate;
using Vole_Papillon_Damour.Domain.BookMovementAggregate.ValueObjects;
using Vole_Papillon_Damour.Domain.EventsAggregate.ValueObjects;
using Vole_Papillon_Damour.Domain.ManualRequestAggregate;
using Vole_Papillon_Damour.Domain.ManualRequestAggregate.ValueObjects;
using Vole_Papillon_Damour.Domain.ScanSessionAggregate;
using Vole_Papillon_Damour.Domain.ScanSessionAggregate.ValueObjects;
using Vole_Papillon_Damour.Domain.UserAggregate.ValueObjects;

namespace Vole_Papillon_Damour.Application.ManualRequests.Common;

public enum ManualRequestReleaseMode
{
    AvailableNow,
    NextFair
}

public sealed record ManualRequestReleasePlan(
    ManualRequestReleaseMode Mode,
    AssoEventsId? FairId,
    DateTimeOffset? FairStart);

internal static class ManualRequestRelease
{
    public static async Task<ManualRequestReleasePlan> PlanAsync(
        IProjectDbContext db,
        ManualBookRequest request,
        DateTime now,
        CancellationToken ct)
    {
        var plans = await PlanManyAsync(db, [request], now, ct);
        return plans[request.Id];
    }

    /// <summary>
    /// Batch variant of <see cref="PlanAsync"/>: at most one query for the scan sessions and one for
    /// the candidate fairs, whatever the number of requests. Same decision rules as <see cref="PlanAsync"/>.
    /// </summary>
    public static async Task<IReadOnlyDictionary<Guid, ManualRequestReleasePlan>> PlanManyAsync(
        IProjectDbContext db,
        IReadOnlyCollection<ManualBookRequest> requests,
        DateTime now,
        CancellationToken ct)
    {
        var sessionIds = requests
            .Where(request => request.FoundOrigin == ManualBookRequestFoundOrigin.Scan &&
                              request.FoundScanSessionId is not null)
            .Select(request => request.FoundScanSessionId!)
            .Distinct()
            .ToArray();
        var sessions = sessionIds.Length == 0
            ? new Dictionary<ScanSessionId, ScanSession>()
            : await db.ScanSessions
                .AsNoTracking()
                .Where(session => sessionIds.Contains(session.Id))
                .ToDictionaryAsync(session => session.Id, ct);

        var fairIds = requests
            .Select(request => CandidateFairId(request, sessions))
            .Where(fairId => fairId is not null)
            .Select(fairId => fairId!)
            .Distinct()
            .ToArray();
        var fairs = fairIds.Length == 0
            ? new Dictionary<AssoEventsId, AssoEvents>()
            : await db.AssoEvents
                .AsNoTracking()
                .WhereActiveBookFair()
                .Where(fair => fairIds.Contains(fair.Id))
                .ToDictionaryAsync(fair => fair.Id, ct);

        var plans = new Dictionary<Guid, ManualRequestReleasePlan>(requests.Count);
        foreach (var request in requests)
        {
            var fairId = CandidateFairId(request, sessions);
            plans[request.Id] = fairId is { } candidate &&
                                fairs.TryGetValue(candidate, out var fair) &&
                                IsFutureFair(fair, now)
                ? ForFair(fair)
                : AvailableNow();
        }

        return plans;
    }

    private static AssoEventsId? CandidateFairId(
        ManualBookRequest request,
        IReadOnlyDictionary<ScanSessionId, ScanSession> sessions)
    {
        if (request.FoundOrigin == ManualBookRequestFoundOrigin.Scan && request.FoundScanSessionId is { } sessionId)
        {
            return sessions.TryGetValue(sessionId, out var session) &&
                   session is { Mode: ScanMode.NextFair, TargetAssoEventsId: { } sessionFairId }
                ? sessionFairId
                : null;
        }

        return request.FoundOrigin == ManualBookRequestFoundOrigin.StockAnnounced
            ? request.PickedFromFairId
            : null;
    }

    public static async Task<ErrorOr<BookMovementId>> ReleaseAsync(
        IProjectDbContext db,
        ManualBookRequest request,
        UserId by,
        DateTime now,
        CancellationToken ct)
    {
        if (request.FoundIsbn13 is not { } foundIsbn)
        {
            return Error.Validation("ManualRequest.FoundBookRequired", "A found request must identify its book before release.");
        }

        BookCode canonicalIsbn = foundIsbn;
        var seen = new HashSet<BookCode>();
        Book? book = null;
        while (seen.Add(canonicalIsbn))
        {
            book = await db.Books.SingleOrDefaultAsync(candidate => candidate.Id == canonicalIsbn, ct);
            if (book is null)
            {
                return Error.NotFound("ManualRequest.BookNotFound", "The found book could not be loaded.");
            }

            if (book.RedirectedToIsbn13 is not { } redirectedIsbn)
            {
                break;
            }

            canonicalIsbn = redirectedIsbn;
            book = null;
        }

        if (book is null)
        {
            return Error.Conflict("ManualRequest.BookRedirectLoop", "The found book has an invalid redirect chain.");
        }

        var plan = await PlanAsync(db, request, now, ct);
        var movementType = plan.Mode switch
        {
            ManualRequestReleaseMode.AvailableNow => BookMovementType.DirectEntry,
            ManualRequestReleaseMode.NextFair => BookMovementType.AnnouncementEntry,
            _ => throw new ArgumentOutOfRangeException(nameof(plan.Mode), plan.Mode, null)
        };

        if (plan.Mode == ManualRequestReleaseMode.AvailableNow)
        {
            book.RecordAvailableEntry(now);
        }
        else
        {
            if (plan.FairId is null || request.FoundScanSessionId is not { } scanSessionId)
            {
                return Error.Validation(
                    "ManualRequest.AnnouncementContextRequired",
                    "A fair release requires both a fair and the original scan session.");
            }

            book.RecordAnnouncementEntry(now);
            db.BookAnnouncements.Add(BookAnnouncement.Create(
                BookAnnouncementId.CreateUnique(),
                isbn13: canonicalIsbn,
                assoEventsId: plan.FairId,
                quantity: 1,
                createdAt: now,
                scanSessionId: scanSessionId,
                clientGestureId: null));
        }

        var movementId = BookMovementId.CreateUnique();
        db.BookMovements.Add(BookMovement.Create(
            movementId,
            canonicalIsbn,
            movementType,
            quantity: 1,
            occurredAt: now,
            receivedAt: now,
            clockSuspect: false,
            scanSessionId: null,
            volunteerId: by,
            assoEventsId: plan.FairId,
            note: "ManualRequest.Release",
            clientGestureId: null));

        return movementId;
    }

    private static bool IsFutureFair(AssoEvents fair, DateTime now) =>
        (fair.DateEnd ?? fair.DateStart) > new DateTimeOffset(DateTime.SpecifyKind(now, DateTimeKind.Utc));

    private static ManualRequestReleasePlan AvailableNow() =>
        new(ManualRequestReleaseMode.AvailableNow, null, null);

    private static ManualRequestReleasePlan ForFair(AssoEvents fair) =>
        new(ManualRequestReleaseMode.NextFair, fair.Id, BookFairSchedule.GetOpeningInstant(fair));
}
