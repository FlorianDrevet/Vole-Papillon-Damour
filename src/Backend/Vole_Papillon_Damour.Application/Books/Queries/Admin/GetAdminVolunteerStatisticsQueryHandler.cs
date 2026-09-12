using ErrorOr;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Vole_Papillon_Damour.Application.Books.Common;
using Vole_Papillon_Damour.Application.Common.Interfaces.Persistence;
using Vole_Papillon_Damour.Application.Common.Interfaces.Services;
using Vole_Papillon_Damour.Domain.AssoEventsAggregate;
using Vole_Papillon_Damour.Domain.BookAggregate;
using Vole_Papillon_Damour.Domain.BookAggregate.ValueObjects;
using Vole_Papillon_Damour.Domain.BookMovementAggregate;
using Vole_Papillon_Damour.Domain.BookMovementAggregate.ValueObjects;
using Vole_Papillon_Damour.Domain.Common.Errors;
using Vole_Papillon_Damour.Domain.EventsAggregate.ValueObjects;
using Vole_Papillon_Damour.Domain.ScanSessionAggregate;
using Vole_Papillon_Damour.Domain.ScanSessionAggregate.ValueObjects;
using Vole_Papillon_Damour.Domain.UserAggregate;
using Vole_Papillon_Damour.Domain.UserAggregate.ValueObjects;

namespace Vole_Papillon_Damour.Application.Books.Queries.Admin;

public sealed class GetAdminVolunteerStatisticsQueryHandler(
    IProjectDbContext dbContext,
    IDateTimeProvider dateTimeProvider)
    : IRequestHandler<GetAdminVolunteerStatisticsQuery, ErrorOr<AdminVolunteerStatisticsResult>>
{
    private static readonly TimeZoneInfo ParisTimeZone = ResolveParisTimeZone();
    private const int RenewalWindowDays = 90;

    public async Task<ErrorOr<AdminVolunteerStatisticsResult>> Handle(
        GetAdminVolunteerStatisticsQuery query,
        CancellationToken cancellationToken)
    {
        var generatedAt = dateTimeProvider.UtcNow;
        if (generatedAt.Kind != DateTimeKind.Utc)
        {
            return Error.Validation("Book.InvalidClock", "The administration clock must be expressed in UTC.");
        }

        var from = query.From?.UtcDateTime;
        var to = query.To?.UtcDateTime;
        if (from is not null && to is not null && from >= to)
        {
            return Error.Validation("Book.InvalidPeriod", "The volunteer statistics period must be valid UTC instants.");
        }

        AssoEvents? selectedFair = null;
        if (query.FairId is { } fairId)
        {
            if (fairId == Guid.Empty)
            {
                return Errors.Book.FairNotFound(fairId);
            }

            selectedFair = await dbContext.AssoEvents
                .AsNoTracking()
                .SingleOrDefaultAsync(candidate => candidate.Id == AssoEventsId.Create(fairId), cancellationToken);
            if (selectedFair is null)
            {
                return Errors.Book.FairNotFound(fairId);
            }

            if (selectedFair.EventsType?.Value != EventsType.EventsTypeEnum.Books)
            {
                return Errors.Book.TargetFairMustBeBooks();
            }
        }

        var sessions = await dbContext.ScanSessions
            .AsNoTracking()
            .Where(session => session.ScannedCount > 0)
            .ToListAsync(cancellationToken);
        sessions = sessions
            .Where(session => IsInPeriod(session.StartedAt, from, to))
            .Where(session => selectedFair is null || session.TargetAssoEventsId == selectedFair.Id)
            .ToList();

        var relevantMovements = await dbContext.BookMovements
            .AsNoTracking()
            .Where(movement =>
                movement.Type == BookMovementType.DirectEntry ||
                movement.Type == BookMovementType.AnnouncementEntry ||
                movement.Type == BookMovementType.Rejection ||
                movement.Type == BookMovementType.Sale ||
                movement.Type == BookMovementType.Correction)
            .ToListAsync(cancellationToken);
        var sessionsById = sessions.ToDictionary(session => session.Id, session => session);
        var movements = relevantMovements
            .Where(movement => IsInPeriod(movement.OccurredAt, from, to))
            .Where(movement => IsInSelectedFair(movement, sessionsById, selectedFair?.Id))
            .ToArray();

        var books = await dbContext.Books
            .AsNoTracking()
            .ToDictionaryAsync(book => book.Id, cancellationToken);
        var volunteers = await dbContext.Users
            .AsNoTracking()
            .ToDictionaryAsync(user => user.Id, cancellationToken);

        var scanMovements = movements
            .Where(movement =>
                movement.ScanSessionId is not null &&
                sessionsById.ContainsKey(movement.ScanSessionId) &&
                movement.Type is BookMovementType.DirectEntry or BookMovementType.AnnouncementEntry or BookMovementType.Rejection)
            .ToArray();
        var sales = BuildEffectiveSales(movements);
        var volunteerIds = sessions.Select(session => session.VolunteerId)
            .Concat(scanMovements.Select(movement => movement.VolunteerId).Where(id => id is not null).Select(id => id!))
            .Concat(sales.Select(sale => sale.Movement.VolunteerId).Where(id => id is not null).Select(id => id!))
            .Distinct()
            .ToArray();

        var contributions = volunteerIds
            .Select(volunteerId => BuildContribution(
                volunteerId,
                generatedAt,
                sessions,
                scanMovements,
                sales,
                books,
                volunteers))
            .OrderByDescending(contribution => contribution.ScannedCount)
            .ThenByDescending(contribution => contribution.SoldQuantity)
            .ThenBy(contribution => contribution.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var scannedCount = sessions.Sum(session => session.ScannedCount);
        var keptCount = sessions.Sum(session => session.KeptCount);
        var rejectedCount = sessions.Sum(session => session.RejectedCount);
        var scanDurationMinutes = contributions.Sum(contribution => contribution.ScanDurationMinutes);
        var cashDurationMinutes = contributions.Sum(contribution => contribution.CashDurationMinutes);
        var soldQuantity = sales.Sum(sale => sale.Quantity);
        var activeVolunteerCount = contributions.Length;
        var team = new AdminVolunteerTeamSummaryResult(
            activeVolunteerCount,
            scannedCount,
            keptCount,
            rejectedCount,
            Percent(keptCount, scannedCount),
            soldQuantity,
            Percent(soldQuantity, keptCount),
            sessions.Count,
            scanDurationMinutes,
            cashDurationMinutes,
            scanDurationMinutes + cashDurationMinutes,
            activeVolunteerCount == 0
                ? null
                : decimal.Round(sessions.Count / (decimal)activeVolunteerCount, 1, MidpointRounding.AwayFromZero));

        return new AdminVolunteerStatisticsResult(
            new DateTimeOffset(generatedAt, TimeSpan.Zero),
            query.From,
            query.To,
            selectedFair?.Id.Value,
            team,
            contributions,
            BuildMonthlyActivity(generatedAt, contributions, sessions),
            BuildRenewal(generatedAt, contributions),
            BuildDominantGenres(scanMovements, books));
    }

    private static AdminVolunteerContributionResult BuildContribution(
        UserId volunteerId,
        DateTime generatedAt,
        IReadOnlyCollection<ScanSession> sessions,
        IReadOnlyCollection<BookMovement> scanMovements,
        IReadOnlyCollection<EffectiveSale> sales,
        IReadOnlyDictionary<Isbn13, Book> books,
        IReadOnlyDictionary<UserId, User> volunteers)
    {
        var volunteerSessions = sessions
            .Where(session => session.VolunteerId == volunteerId)
            .ToArray();
        var sessionIds = volunteerSessions.Select(session => session.Id).ToHashSet();
        var volunteerScans = scanMovements
            .Where(movement => movement.ScanSessionId is not null && sessionIds.Contains(movement.ScanSessionId))
            .ToArray();
        var keptScans = volunteerScans
            .Where(IsKeptScan)
            .ToArray();
        var volunteerSales = sales
            .Where(sale => sale.Movement.VolunteerId == volunteerId)
            .ToArray();
        var keptByIsbn = keptScans
            .GroupBy(movement => movement.Isbn13)
            .ToDictionary(group => group.Key, group => group.Sum(movement => Math.Abs(movement.Quantity)));
        var soldByIsbn = sales
            .GroupBy(sale => sale.Movement.Isbn13)
            .ToDictionary(group => group.Key, group => group.Sum(sale => sale.Quantity));
        var waitingQuantity = keptByIsbn.Sum(item => Math.Max(0, item.Value - soldByIsbn.GetValueOrDefault(item.Key)));
        var waitingOverYearQuantity = keptScans
            .Where(movement => movement.OccurredAt <= generatedAt.AddYears(-1))
            .GroupBy(movement => movement.Isbn13)
            .Sum(group => Math.Max(0, group.Sum(movement => Math.Abs(movement.Quantity)) - soldByIsbn.GetValueOrDefault(group.Key)));
        var displayName = volunteers.TryGetValue(volunteerId, out var volunteer)
            ? GetDisplayName(volunteer, volunteerId)
            : $"Bénévole {volunteerId.Value.ToString()[..8]}";
        var hasTriActivity = volunteerSessions.Any(session => session.ScannedCount > 0) || keptScans.Length > 0;
        var hasCashActivity = volunteerSales.Length > 0;
        var roles = new List<string>(2);
        if (hasTriActivity) roles.Add("Tri");
        if (hasCashActivity) roles.Add("Caisse");
        var activities = volunteerSessions.Select(session => session.StartedAt)
            .Concat(volunteerScans.Select(movement => movement.OccurredAt))
            .Concat(volunteerSales.Select(sale => sale.Movement.OccurredAt))
            .ToArray();
        DateTime? firstActivityAt = activities.Length == 0 ? null : activities.Min();
        DateTime? lastActivityAt = activities.Length == 0 ? null : activities.Max();

        return new AdminVolunteerContributionResult(
            volunteerId.Value,
            displayName,
            roles,
            volunteerSessions.Length,
            volunteerSessions.Sum(GetSessionDurationMinutes),
            BuildCashDurationMinutes(volunteerSales),
            volunteerSessions.Sum(GetSessionDurationMinutes) + BuildCashDurationMinutes(volunteerSales),
            volunteerSessions.Sum(session => session.ScannedCount),
            volunteerSessions.Sum(session => session.KeptCount),
            Percent(
                volunteerSessions.Sum(session => session.KeptCount),
                volunteerSessions.Sum(session => session.ScannedCount)),
            volunteerSales.Sum(sale => sale.Quantity),
            waitingQuantity,
            waitingOverYearQuantity,
            Percent(
                Math.Max(0, keptScans.Sum(movement => Math.Abs(movement.Quantity)) - waitingQuantity),
                keptScans.Sum(movement => Math.Abs(movement.Quantity))),
            firstActivityAt is null ? null : new DateTimeOffset(firstActivityAt.Value, TimeSpan.Zero),
            lastActivityAt is null ? null : new DateTimeOffset(lastActivityAt.Value, TimeSpan.Zero),
            BuildDominantGenre(keptScans, books));
    }

    private static IReadOnlyList<AdminVolunteerMonthlyActivityResult> BuildMonthlyActivity(
        DateTime generatedAt,
        IReadOnlyCollection<AdminVolunteerContributionResult> contributions,
        IReadOnlyCollection<ScanSession> sessions)
    {
        var firstMonth = new DateTime(
            generatedAt.Year,
            generatedAt.Month,
            1,
            0,
            0,
            0,
            DateTimeKind.Utc).AddMonths(-11);
        return contributions
            .Select(contribution => new AdminVolunteerMonthlyActivityResult(
                contribution.VolunteerId,
                contribution.DisplayName,
                Enumerable.Range(0, 12)
                    .Select(offset =>
                    {
                        var month = firstMonth.AddMonths(offset);
                        var nextMonth = month.AddMonths(1);
                        var count = sessions.Count(session =>
                            session.VolunteerId.Value == contribution.VolunteerId &&
                            session.StartedAt >= month &&
                            session.StartedAt < nextMonth);
                        return new AdminVolunteerMonthResult(new DateTimeOffset(month, TimeSpan.Zero), count);
                    })
                    .ToArray()))
            .ToArray();
    }

    private static AdminVolunteerRenewalResult BuildRenewal(
        DateTime generatedAt,
        IReadOnlyCollection<AdminVolunteerContributionResult> contributions)
    {
        var newCutoff = generatedAt.AddDays(-RenewalWindowDays);
        var withdrawingCutoff = generatedAt.AddDays(-30);
        var newCount = contributions.Count(contribution =>
            contribution.FirstActivityAt is not null && contribution.FirstActivityAt.Value.UtcDateTime >= newCutoff);
        var withdrawingCount = contributions.Count(contribution =>
            contribution.FirstActivityAt is not null &&
            contribution.FirstActivityAt.Value.UtcDateTime < newCutoff &&
            contribution.LastActivityAt is not null &&
            contribution.LastActivityAt.Value.UtcDateTime < withdrawingCutoff);
        return new AdminVolunteerRenewalResult(
            newCount,
            contributions.Count - newCount - withdrawingCount,
            withdrawingCount,
            RenewalWindowDays);
    }

    private static IReadOnlyList<AdminVolunteerGenreResult> BuildDominantGenres(
        IReadOnlyCollection<BookMovement> scanMovements,
        IReadOnlyDictionary<Isbn13, Book> books)
    {
        return scanMovements
            .Where(IsKeptScan)
            .GroupBy(movement => GetGenre(movement, books), StringComparer.OrdinalIgnoreCase)
            .Select(group => new AdminVolunteerGenreResult(group.Key, group.Sum(movement => Math.Abs(movement.Quantity))))
            .OrderByDescending(item => item.Quantity)
            .ThenBy(item => item.Name, StringComparer.OrdinalIgnoreCase)
            .Take(8)
            .ToArray();
    }

    private static string? BuildDominantGenre(
        IReadOnlyCollection<BookMovement> keptScans,
        IReadOnlyDictionary<Isbn13, Book> books)
    {
        return keptScans
            .GroupBy(movement => GetGenre(movement, books), StringComparer.OrdinalIgnoreCase)
            .OrderByDescending(group => group.Sum(movement => Math.Abs(movement.Quantity)))
            .ThenBy(group => group.Key, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.Key)
            .FirstOrDefault();
    }

    private static string GetGenre(BookMovement movement, IReadOnlyDictionary<Isbn13, Book> books) =>
        books.TryGetValue(movement.Isbn13, out var book) && !string.IsNullOrWhiteSpace(book.Genre)
            ? book.Genre!
            : "Sans genre";

    private static IReadOnlyList<EffectiveSale> BuildEffectiveSales(IReadOnlyCollection<BookMovement> movements)
    {
        var sales = movements
            .Where(movement => movement.Type == BookMovementType.Sale && movement.Quantity < 0)
            .ToArray();
        var saleIds = sales.Select(sale => sale.Id.Value).ToHashSet();
        var voidedBySale = movements
            .Where(movement =>
                movement.Type == BookMovementType.Correction &&
                movement.ReversalOfMovementId is not null &&
                saleIds.Contains(movement.ReversalOfMovementId.Value))
            .GroupBy(movement => movement.ReversalOfMovementId!.Value)
            .ToDictionary(group => group.Key, group => group.Sum(movement => Math.Abs(movement.Quantity)));
        return sales
            .Select(sale => new EffectiveSale(
                sale,
                Math.Max(0, Math.Abs(sale.Quantity) - voidedBySale.GetValueOrDefault(sale.Id.Value))))
            .Where(sale => sale.Quantity > 0)
            .ToArray();
    }

    private static bool IsInPeriod(DateTime value, DateTime? from, DateTime? to) =>
        (from is null || value >= from.Value) && (to is null || value < to.Value);

    private static bool IsInSelectedFair(
        BookMovement movement,
        IReadOnlyDictionary<ScanSessionId, ScanSession> sessions,
        AssoEventsId? fairId)
    {
        if (fairId is null)
        {
            return true;
        }

        if (movement.AssoEventsId == fairId)
        {
            return true;
        }

        return movement.ScanSessionId is { } sessionId &&
               sessions.TryGetValue(sessionId, out var session) &&
               session.TargetAssoEventsId == fairId;
    }

    private static bool IsKeptScan(BookMovement movement) =>
        movement.Type is BookMovementType.DirectEntry or BookMovementType.AnnouncementEntry;

    private static decimal? Percent(int numerator, int denominator) =>
        denominator == 0
            ? null
            : decimal.Round(numerator * 100m / denominator, 1, MidpointRounding.AwayFromZero);

    private static int GetSessionDurationMinutes(ScanSession session)
    {
        var end = session.EndedAt ?? session.LastScanAt;
        return Math.Max(0, (int)Math.Ceiling((end - session.StartedAt).TotalMinutes));
    }

    private static int BuildCashDurationMinutes(IEnumerable<EffectiveSale> sales)
    {
        return sales
            .GroupBy(sale => new
            {
                FairId = sale.Movement.AssoEventsId?.Value,
                Day = DateOnly.FromDateTime(ToParis(sale.Movement.OccurredAt).DateTime),
            })
            .Sum(group =>
            {
                var first = group.Min(sale => sale.Movement.OccurredAt);
                var last = group.Max(sale => sale.Movement.OccurredAt);
                return Math.Max(15, (int)Math.Ceiling((last - first).TotalMinutes));
            });
    }

    private static string GetDisplayName(User user, UserId userId)
    {
        var name = user.Name is null ? null : $"{user.Name.FirstName} {user.Name.LastName}".Trim();
        return string.IsNullOrWhiteSpace(name)
            ? $"Bénévole {userId.Value.ToString()[..8]}"
            : name;
    }

    private static DateTimeOffset ToParis(DateTime utc) =>
        TimeZoneInfo.ConvertTime(new DateTimeOffset(utc, TimeSpan.Zero), ParisTimeZone);

    private static TimeZoneInfo ResolveParisTimeZone()
    {
        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById("Europe/Paris");
        }
        catch (TimeZoneNotFoundException)
        {
            return TimeZoneInfo.FindSystemTimeZoneById("Romance Standard Time");
        }
    }

    private sealed record EffectiveSale(BookMovement Movement, int Quantity);
}
