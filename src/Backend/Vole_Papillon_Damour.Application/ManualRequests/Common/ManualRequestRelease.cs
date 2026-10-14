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
        if (request.FoundOrigin == ManualBookRequestFoundOrigin.Scan && request.FoundScanSessionId is { } sessionId)
        {
            var session = await db.ScanSessions
                .AsNoTracking()
                .SingleOrDefaultAsync(candidate => candidate.Id == sessionId, ct);
            if (session is { Mode: ScanMode.NextFair, TargetAssoEventsId: { } sessionFairId })
            {
                var fair = await FindFutureActiveFairAsync(db, sessionFairId, now, ct);
                if (fair is not null)
                {
                    return ForFair(fair);
                }
            }

            return AvailableNow();
        }

        if (request.FoundOrigin == ManualBookRequestFoundOrigin.StockAnnounced &&
            request.PickedFromFairId is { } pickedFairId)
        {
            var fair = await FindFutureActiveFairAsync(db, pickedFairId, now, ct);
            if (fair is not null)
            {
                return ForFair(fair);
            }
        }

        return AvailableNow();
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

        var canonicalIsbn = foundIsbn;
        var seen = new HashSet<Isbn13>();
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

    private static async Task<AssoEvents?> FindFutureActiveFairAsync(
        IProjectDbContext db,
        AssoEventsId fairId,
        DateTime now,
        CancellationToken ct)
    {
        var fair = await db.AssoEvents
            .AsNoTracking()
            .WhereActiveBookFair()
            .SingleOrDefaultAsync(candidate => candidate.Id == fairId, ct);
        if (fair is null || (fair.DateEnd ?? fair.DateStart) <= new DateTimeOffset(DateTime.SpecifyKind(now, DateTimeKind.Utc)))
        {
            return null;
        }

        return fair;
    }

    private static ManualRequestReleasePlan AvailableNow() =>
        new(ManualRequestReleaseMode.AvailableNow, null, null);

    private static ManualRequestReleasePlan ForFair(AssoEvents fair) =>
        new(ManualRequestReleaseMode.NextFair, fair.Id, BookFairSchedule.GetOpeningInstant(fair));
}
