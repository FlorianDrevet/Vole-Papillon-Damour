using ErrorOr;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Vole_Papillon_Damour.Application.Books.Common;
using Vole_Papillon_Damour.Application.Common.Interfaces.Persistence;
using Vole_Papillon_Damour.Application.Common.Interfaces.Services;
using Vole_Papillon_Damour.Domain.BookAggregate.ValueObjects;
using Vole_Papillon_Damour.Domain.BookMovementAggregate.ValueObjects;
using Vole_Papillon_Damour.Domain.EventsAggregate.ValueObjects;
using Vole_Papillon_Damour.Domain.UserAggregate.ValueObjects;
using Vole_Papillon_Damour.Domain.RareBookAggregate.ValueObjects;

namespace Vole_Papillon_Damour.Application.Books.Queries.GetVolunteerStatistics;

public sealed class GetVolunteerStatisticsQueryHandler(
    IProjectDbContext dbContext,
    IDateTimeProvider dateTimeProvider,
    IBookAlertOutbox bookAlertOutbox)
    : IRequestHandler<GetVolunteerStatisticsQuery, ErrorOr<VolunteerStatisticsResult>>
{
    private static readonly TimeZoneInfo ParisTimeZone = ResolveParisTimeZone();

    public async Task<ErrorOr<VolunteerStatisticsResult>> Handle(
        GetVolunteerStatisticsQuery query,
        CancellationToken cancellationToken)
    {
        if (query.VolunteerId is null || query.VolunteerId.Value == Guid.Empty)
        {
            return Error.Validation(
                "Book.InvalidVolunteerId",
                "A volunteer identifier is required.");
        }

        var generatedAt = dateTimeProvider.UtcNow;
        if (generatedAt.Kind != DateTimeKind.Utc)
        {
            return Error.Validation(
                "Book.InvalidClock",
                "The volunteer statistics clock must be expressed in UTC.");
        }

        var volunteerId = query.VolunteerId;
        var sessions = await dbContext.ScanSessions
            .AsNoTracking()
            .Where(session => session.VolunteerId == volunteerId && session.ScannedCount > 0)
            .OrderByDescending(session => session.StartedAt)
            .ThenByDescending(session => session.Id)
            .Select(StatisticsProjectionExpressions.ScanSession)
            .ToListAsync(cancellationToken);
        var allSessions = await dbContext.ScanSessions
            .AsNoTracking()
            .Where(session => session.ScannedCount > 0)
            .Select(session => new StatisticsSessionKeptRateProjection(
                session.KeptCount,
                session.ScannedCount))
            .ToListAsync(cancellationToken);
        var sessionIds = sessions.Select(session => session.Id.Value).ToHashSet();

        var volunteerMovements = await dbContext.BookMovements
            .AsNoTracking()
            .Where(movement => movement.VolunteerId == volunteerId)
            .Select(StatisticsProjectionExpressions.BookMovement)
            .ToListAsync(cancellationToken);
        var allSalesAndCorrections = await dbContext.BookMovements
            .AsNoTracking()
            .Where(movement =>
                movement.Type == BookMovementType.Sale ||
                movement.Type == BookMovementType.Correction)
            .Select(StatisticsProjectionExpressions.BookMovement)
            .ToListAsync(cancellationToken);
        var rareBookIsbns = (await dbContext.RareBooks
                .AsNoTracking()
                .Where(rareBook =>
                    rareBook.CreatedBy == volunteerId &&
                    rareBook.Isbn13 != null)
                .Select(rareBook => rareBook.Isbn13!.Value.Value)
                .ToListAsync(cancellationToken))
            .ToHashSet(StringComparer.Ordinal);
        var fairs = (await dbContext.AssoEvents
            .AsNoTracking()
            .Select(fair => new VolunteerFairProjection(
                fair.Id,
                fair.Name,
                fair.DateStart,
                fair.BookRevenue))
            .ToListAsync(cancellationToken))
            .ToDictionary(fair => fair.Id.Value);
        var memberSince = await dbContext.Users
            .AsNoTracking()
            .Where(user => user.Id == volunteerId)
            .Select(user => (DateTime?)user.CreatedAt)
            .SingleOrDefaultAsync(cancellationToken);

        var personalScanMovements = volunteerMovements
            .Where(movement =>
                movement.ScanSessionId is not null &&
                sessionIds.Contains(movement.ScanSessionId.Value) &&
                movement.Quantity > 0)
            .ToArray();
        var keptMovements = personalScanMovements
            .Where(IsKeptScan)
            .ToArray();
        var rejectedMovements = personalScanMovements
            .Where(movement => movement.Type == BookMovementType.Rejection)
            .ToArray();
        var personalSales = volunteerMovements
            .Where(movement =>
                movement.Type == BookMovementType.Sale &&
                movement.Quantity < 0)
            .ToArray();
        var personalSaleIds = personalSales.Select(sale => sale.Id.Value).ToHashSet();
        var personalReversals = allSalesAndCorrections
            .Where(movement =>
                movement.Type == BookMovementType.Correction &&
                movement.ReversalOfMovementId is not null &&
                personalSaleIds.Contains(movement.ReversalOfMovementId.Value))
            .ToArray();
        var correctionsBySale = personalReversals
            .Where(movement => movement.ReversalOfMovementId is not null)
            .GroupBy(movement => movement.ReversalOfMovementId!.Value)
            .ToDictionary(group => group.Key, group => group.ToArray());

        var keptIsbns = keptMovements.Select(movement => movement.Isbn13).Distinct().ToArray();
        var firstEntryByIsbn = keptIsbns.Length == 0
            ? new Dictionary<string, BookMovementId>(StringComparer.Ordinal)
            : (await dbContext.BookMovements
                .AsNoTracking()
                .Where(movement =>
                    (movement.Type == BookMovementType.DirectEntry ||
                     movement.Type == BookMovementType.AnnouncementEntry) &&
                    keptIsbns.Contains(movement.Isbn13))
                .GroupBy(movement => movement.Isbn13)
                .Select(group => new
                {
                    Isbn13 = group.Key,
                    FirstEntryId = group
                        .OrderBy(movement => movement.OccurredAt)
                        .ThenBy(movement => movement.Id)
                        .Select(movement => movement.Id)
                        .First(),
                })
                .ToDictionaryAsync(item => item.Isbn13.Value, item => item.FirstEntryId, cancellationToken));
        var soldIsbns = keptIsbns.Length == 0
            ? new HashSet<string>(StringComparer.Ordinal)
            : (await dbContext.BookMovements
                .AsNoTracking()
                .Where(movement =>
                    movement.Type == BookMovementType.Sale &&
                    movement.Quantity < 0 &&
                    keptIsbns.Contains(movement.Isbn13))
                .Select(movement => movement.Isbn13.Value)
                .Distinct()
                .ToListAsync(cancellationToken))
                .ToHashSet(StringComparer.Ordinal);
        var bookIsbns = keptMovements
            .Concat(personalSales)
            .Select(movement => movement.Isbn13)
            .Distinct()
            .ToArray();
        var books = bookIsbns.Length == 0
            ? new Dictionary<string, StatisticsBookProjection>(StringComparer.Ordinal)
            : (await dbContext.Books
                .AsNoTracking()
                .Where(book => bookIsbns.Contains(book.Id))
                .Select(StatisticsProjectionExpressions.PersonalVolunteerBook)
                .ToListAsync(cancellationToken))
                .ToDictionary(book => book.Isbn13.Value, StringComparer.Ordinal);

        var alertItemCount = await bookAlertOutbox.GetSentItemCountForSessionsAsync(
            sessionIds,
            cancellationToken);
        var scan = BuildScanStatistics(
            generatedAt,
            sessions,
            allSessions,
            personalScanMovements,
            keptMovements,
            rejectedMovements,
            firstEntryByIsbn,
            soldIsbns,
            allSalesAndCorrections,
            books,
            rareBookIsbns,
            alertItemCount);
        var cash = BuildCashStatistics(
            generatedAt,
            personalSales,
            correctionsBySale,
            allSalesAndCorrections,
            keptMovements,
            books,
            fairs);

        return new VolunteerStatisticsResult(
            new DateTimeOffset(generatedAt, TimeSpan.Zero),
            memberSince is null ? null : new DateTimeOffset(memberSince.Value, TimeSpan.Zero),
            scan,
            cash);
    }

    private static VolunteerScanStatisticsResult BuildScanStatistics(
        DateTime generatedAt,
        IReadOnlyCollection<StatisticsScanSessionProjection> sessions,
        IReadOnlyCollection<StatisticsSessionKeptRateProjection> allSessions,
        IReadOnlyCollection<StatisticsBookMovementProjection> personalScanMovements,
        IReadOnlyCollection<StatisticsBookMovementProjection> keptMovements,
        IReadOnlyCollection<StatisticsBookMovementProjection> rejectedMovements,
        IReadOnlyDictionary<string, BookMovementId> firstEntryByIsbn,
        IReadOnlySet<string> soldIsbns,
        IReadOnlyCollection<StatisticsBookMovementProjection> allSalesAndCorrections,
        IReadOnlyDictionary<string, StatisticsBookProjection> books,
        IReadOnlySet<string> rareBookIsbns,
        int alertItemCount)
    {
        var monthly = BuildMonthlyStatistics(
            generatedAt,
            keptMovements,
            rejectedMovements);
        var sessionStatistics = sessions
            .OrderByDescending(session => session.StartedAt)
            .ThenByDescending(session => session.Id.Value)
            .Take(6)
            .Select(ToSessionStatistics)
            .ToArray();
        var newTitleCount = keptMovements
            .Where(movement =>
                firstEntryByIsbn.TryGetValue(movement.Isbn13.Value, out var firstId) &&
                firstId == movement.Id)
            .Select(movement => movement.Isbn13.Value)
            .Distinct(StringComparer.Ordinal)
            .Count();
        var foundReaderCount = keptMovements
            .Where(movement => soldIsbns.Contains(movement.Isbn13.Value))
            .Select(movement => movement.Isbn13.Value)
            .Distinct(StringComparer.Ordinal)
            .Count();
        var rareCount = keptMovements
            .Where(movement => rareBookIsbns.Contains(movement.Isbn13.Value))
            .Select(movement => movement.Isbn13.Value)
            .Distinct(StringComparer.Ordinal)
            .Count();

        return new VolunteerScanStatisticsResult(
            sessions.Sum(session => session.ScannedCount),
            sessions.Sum(session => session.KeptCount),
            sessions.Sum(session => session.RejectedCount),
            sessions.Count,
            sessions.Sum(GetSessionDurationMinutes),
            sessions.Count == 0
                ? null
                : new DateTimeOffset(sessions.Min(session => session.StartedAt), TimeSpan.Zero),
            Median(allSessions
                .Where(session => session.ScannedCount > 0)
                .Select(session => session.KeptCount * 100m / session.ScannedCount)),
            monthly,
            BuildTimeSlots(personalScanMovements),
            new VolunteerImpactStatisticsResult(
                foundReaderCount,
                newTitleCount,
                rareCount,
                alertItemCount,
                FoundReaderIsEstimated: true),
            BuildGenres(keptMovements, books),
            sessionStatistics);
    }

    private static VolunteerCashStatisticsResult BuildCashStatistics(
        DateTime generatedAt,
        IReadOnlyCollection<StatisticsBookMovementProjection> personalSales,
        IReadOnlyDictionary<Guid, StatisticsBookMovementProjection[]> correctionsBySale,
        IReadOnlyCollection<StatisticsBookMovementProjection> allSalesAndCorrections,
        IReadOnlyCollection<StatisticsBookMovementProjection> keptMovements,
        IReadOnlyDictionary<string, StatisticsBookProjection> books,
        IReadOnlyDictionary<Guid, VolunteerFairProjection> fairs)
    {
        var grossSoldQuantity = personalSales.Sum(sale => Math.Abs(sale.Quantity));
        var voidedSaleQuantity = correctionsBySale.Values
            .SelectMany(corrections => corrections)
            .Sum(correction => Math.Abs(correction.Quantity));
        var effectiveSales = personalSales
            .Select(sale => new EffectiveSale(
                sale,
                Math.Max(0, Math.Abs(sale.Quantity) -
                    correctionsBySale.GetValueOrDefault(sale.Id.Value, []).Sum(correction => Math.Abs(correction.Quantity)))))
            .Where(sale => sale.Quantity > 0)
            .ToArray();
        var teamSales = allSalesAndCorrections
            .Where(movement => movement.Type == BookMovementType.Sale && movement.Quantity < 0)
            .ToArray();
        var teamCadences = teamSales
            .Where(sale => sale.VolunteerId is not null)
            .GroupBy(sale => sale.VolunteerId!.Value)
            .Select(group => BuildNetSaleQuantity(group, allSalesAndCorrections) * 60m /
                BuildCashDurationMinutes(group))
            .Where(cadence => cadence > 0)
            .ToArray();
        var fairBreakdown = effectiveSales
            .Where(sale => sale.Movement.AssoEventsId is not null &&
                           fairs.ContainsKey(sale.Movement.AssoEventsId.Value))
            .GroupBy(sale => sale.Movement.AssoEventsId!.Value)
            .Select(group =>
            {
                var fair = fairs[group.Key];
                return new VolunteerFairStatisticsResult(
                    fair.Id.Value,
                    fair.Name,
                    fair.DateStart,
                    group.Sum(sale => sale.Quantity));
            })
            .OrderBy(fair => fair.DateStart)
            .ToArray();
        var cashDurationMinutes = BuildCashDurationMinutes(personalSales);
        var revenueShare = BuildRevenueShare(
            generatedAt,
            effectiveSales,
            allSalesAndCorrections,
            fairs);
        var peak = BuildPeak(personalSales, fairs);
        var netSoldQuantity = effectiveSales.Sum(sale => sale.Quantity);

        return new VolunteerCashStatisticsResult(
            grossSoldQuantity,
            netSoldQuantity,
            personalSales.Count,
            voidedSaleQuantity,
            fairBreakdown.Length,
            cashDurationMinutes,
            cashDurationMinutes == 0
                ? null
                : (int)Math.Round(netSoldQuantity * 60m / cashDurationMinutes, MidpointRounding.AwayFromZero),
            MedianInt(teamCadences),
            fairBreakdown,
            peak,
            BuildTopBooks(effectiveSales, books),
            BuildSaleGenres(effectiveSales, books),
            keptMovements
                .Select(movement => movement.Isbn13.Value)
                .Intersect(effectiveSales.Select(sale => sale.Movement.Isbn13.Value), StringComparer.Ordinal)
                .Distinct(StringComparer.Ordinal)
                .Count(),
            revenueShare?.EstimatedShare,
            revenueShare,
            DurationIsEstimated: true,
            CadenceIsEstimated: true,
            RevenueShareIsEstimated: true,
            TriAndCashOverlapIsEstimated: true);
    }

    private static IReadOnlyList<VolunteerBookStatisticsResult> BuildTopBooks(
        IReadOnlyCollection<EffectiveSale> sales,
        IReadOnlyDictionary<string, StatisticsBookProjection> books)
    {
        return sales
            .GroupBy(sale => sale.Movement.Isbn13.Value, StringComparer.Ordinal)
            .Select(group => new VolunteerBookStatisticsResult(
                group.Key,
                books.TryGetValue(group.Key, out var book) && !string.IsNullOrWhiteSpace(book.Title)
                    ? book.Title!
                    : group.Key,
                group.Sum(sale => sale.Quantity)))
            .OrderByDescending(book => book.Quantity)
            .ThenBy(book => book.Title, StringComparer.OrdinalIgnoreCase)
            .Take(5)
            .ToArray();
    }

    private static VolunteerRevenueShareResult? BuildRevenueShare(
        DateTime generatedAt,
        IReadOnlyCollection<EffectiveSale> personalSales,
        IReadOnlyCollection<StatisticsBookMovementProjection> allSalesAndCorrections,
        IReadOnlyDictionary<Guid, VolunteerFairProjection> fairs)
    {
        var candidate = fairs.Values
            .Where(fair => fair.BookRevenue is not null && fair.DateStart.UtcDateTime <= generatedAt)
            .OrderByDescending(fair => fair.DateStart)
            .FirstOrDefault();
        if (candidate is null)
        {
            return null;
        }

        var fairSales = allSalesAndCorrections
            .Where(movement =>
                movement.Type == BookMovementType.Sale &&
                movement.Quantity < 0 &&
                movement.AssoEventsId?.Value == candidate.Id.Value)
            .ToArray();
        var fairSaleIds = fairSales.Select(sale => sale.Id.Value).ToHashSet();
        var fairCorrections = allSalesAndCorrections
            .Where(movement =>
                movement.Type == BookMovementType.Correction &&
                movement.ReversalOfMovementId is not null &&
                fairSaleIds.Contains(movement.ReversalOfMovementId.Value))
            .Sum(movement => Math.Abs(movement.Quantity));
        var fairSoldQuantity = Math.Max(0, fairSales.Sum(sale => Math.Abs(sale.Quantity)) - fairCorrections);
        if (fairSoldQuantity == 0)
        {
            return null;
        }

        var volunteerSoldQuantity = personalSales
            .Where(sale => sale.Movement.AssoEventsId?.Value == candidate.Id.Value)
            .Sum(sale => sale.Quantity);
        var estimatedShare = decimal.Round(
            candidate.BookRevenue!.Value * volunteerSoldQuantity / fairSoldQuantity,
            2,
            MidpointRounding.AwayFromZero);
        return new VolunteerRevenueShareResult(
            candidate.Id.Value,
            candidate.Name,
            candidate.DateStart,
            candidate.BookRevenue.Value,
            volunteerSoldQuantity,
            fairSoldQuantity,
            estimatedShare);
    }

    private static VolunteerPeakStatisticsResult BuildPeak(
        IReadOnlyCollection<StatisticsBookMovementProjection> sales,
        IReadOnlyDictionary<Guid, VolunteerFairProjection> fairs)
    {
        var peak = sales
            .GroupBy(sale => ToParis(sale.OccurredAt).Hour)
            .Select(group => new
            {
                Hour = group.Key,
                Quantity = group.Sum(sale => Math.Abs(sale.Quantity)),
                Sale = group.OrderBy(sale => sale.OccurredAt).First(),
            })
            .OrderByDescending(group => group.Quantity)
            .ThenBy(group => group.Hour)
            .FirstOrDefault();
        return peak is null
            ? new VolunteerPeakStatisticsResult(null, null, 0)
            : new VolunteerPeakStatisticsResult(
                peak.Sale.AssoEventsId is { } fairId && fairs.TryGetValue(fairId.Value, out var fair)
                    ? fair.DateStart
                    : null,
                peak.Hour,
                peak.Quantity);
    }

    private static int BuildNetSaleQuantity(
        IEnumerable<StatisticsBookMovementProjection> sales,
        IReadOnlyCollection<StatisticsBookMovementProjection> allSalesAndCorrections)
    {
        var saleArray = sales.ToArray();
        var ids = saleArray.Select(sale => sale.Id.Value).ToHashSet();
        var voided = allSalesAndCorrections
            .Where(movement =>
                movement.Type == BookMovementType.Correction &&
                movement.ReversalOfMovementId is not null &&
                ids.Contains(movement.ReversalOfMovementId.Value))
            .Sum(movement => Math.Abs(movement.Quantity));
        return Math.Max(0, saleArray.Sum(sale => Math.Abs(sale.Quantity)) - voided);
    }

    private static int BuildCashDurationMinutes(IEnumerable<StatisticsBookMovementProjection> sales)
    {
        var salesByDay = sales
            .Where(sale => sale.Quantity < 0)
            .GroupBy(sale => new
            {
                FairId = sale.AssoEventsId?.Value,
                Day = DateOnly.FromDateTime(ToParis(sale.OccurredAt).DateTime),
            });
        return salesByDay.Sum(group =>
        {
            var first = group.Min(sale => sale.OccurredAt);
            var last = group.Max(sale => sale.OccurredAt);
            return Math.Max(15, (int)Math.Ceiling((last - first).TotalMinutes));
        });
    }

    private static IReadOnlyList<VolunteerMonthlyStatisticsResult> BuildMonthlyStatistics(
        DateTime generatedAt,
        IReadOnlyCollection<StatisticsBookMovementProjection> keptMovements,
        IReadOnlyCollection<StatisticsBookMovementProjection> rejectedMovements)
    {
        var firstMonth = new DateTime(
            generatedAt.Year,
            generatedAt.Month,
            1,
            0,
            0,
            0,
            DateTimeKind.Utc).AddMonths(-11);
        return Enumerable.Range(0, 12)
            .Select(offset =>
            {
                var start = firstMonth.AddMonths(offset);
                var end = start.AddMonths(1);
                return new VolunteerMonthlyStatisticsResult(
                    new DateTimeOffset(start, TimeSpan.Zero),
                    keptMovements.Count(movement => movement.OccurredAt >= start && movement.OccurredAt < end),
                    rejectedMovements.Count(movement => movement.OccurredAt >= start && movement.OccurredAt < end));
            })
            .ToArray();
    }

    private static IReadOnlyList<VolunteerTimeSlotStatisticsResult> BuildTimeSlots(
        IReadOnlyCollection<StatisticsBookMovementProjection> scanMovements)
    {
        var counts = scanMovements
            .GroupBy(movement =>
            {
                var local = ToParis(movement.OccurredAt);
                return new {DayOfWeek = (int)local.DayOfWeek, Slot = ToSlot(local.Hour)};
            })
            .ToDictionary(group => (group.Key.DayOfWeek, group.Key.Slot), group => group.Count());
        return Enumerable.Range(0, 7)
            .SelectMany(day => new[] {"morning", "afternoon", "evening"}
                .Select(slot => new VolunteerTimeSlotStatisticsResult(
                    day,
                    slot,
                    counts.GetValueOrDefault((day, slot), 0))))
            .ToArray();
    }

    private static IReadOnlyList<VolunteerGenreStatisticsResult> BuildGenres(
        IReadOnlyCollection<StatisticsBookMovementProjection> movements,
        IReadOnlyDictionary<string, StatisticsBookProjection> books)
    {
        return movements
            .GroupBy(movement => books.TryGetValue(movement.Isbn13.Value, out var book) &&
                                 !string.IsNullOrWhiteSpace(book.Genre)
                ? book.Genre!
                : "Sans genre", StringComparer.OrdinalIgnoreCase)
            .Select(group => new VolunteerGenreStatisticsResult(
                group.First().TryGetGenre(books),
                group.Sum(movement => Math.Abs(movement.Quantity))))
            .OrderByDescending(genre => genre.Quantity)
            .ThenBy(genre => genre.Name, StringComparer.OrdinalIgnoreCase)
            .Take(5)
            .ToArray();
    }

    private static IReadOnlyList<VolunteerGenreStatisticsResult> BuildSaleGenres(
        IReadOnlyCollection<EffectiveSale> sales,
        IReadOnlyDictionary<string, StatisticsBookProjection> books)
    {
        return sales
            .GroupBy(sale => books.TryGetValue(sale.Movement.Isbn13.Value, out var book) &&
                             !string.IsNullOrWhiteSpace(book.Genre)
                ? book.Genre!
                : "Sans genre", StringComparer.OrdinalIgnoreCase)
            .Select(group => new VolunteerGenreStatisticsResult(
                group.Key,
                group.Sum(sale => sale.Quantity)))
            .OrderByDescending(genre => genre.Quantity)
            .ThenBy(genre => genre.Name, StringComparer.OrdinalIgnoreCase)
            .Take(5)
            .ToArray();
    }

    private static VolunteerSessionStatisticsResult ToSessionStatistics(StatisticsScanSessionProjection session)
    {
        var keptRate = session.ScannedCount == 0
            ? 0
            : decimal.Round(session.KeptCount * 100m / session.ScannedCount, 1);
        return new VolunteerSessionStatisticsResult(
            session.Id.Value,
            new DateTimeOffset(session.StartedAt, TimeSpan.Zero),
            GetSessionDurationMinutes(session),
            session.ScannedCount,
            keptRate,
            session.Mode.ToString());
    }

    private static int GetSessionDurationMinutes(StatisticsScanSessionProjection session)
    {
        var end = session.EndedAt ?? session.LastScanAt;
        return Math.Max(0, (int)Math.Ceiling((end - session.StartedAt).TotalMinutes));
    }

    private static bool IsKeptScan(StatisticsBookMovementProjection movement) =>
        movement.Type is BookMovementType.DirectEntry or BookMovementType.AnnouncementEntry or
            BookMovementType.ManualRequestHold;

    private static decimal? Median(IEnumerable<decimal> values)
    {
        var ordered = values.OrderBy(value => value).ToArray();
        if (ordered.Length == 0)
        {
            return null;
        }

        var middle = ordered.Length / 2;
        var value = ordered.Length % 2 == 0
            ? (ordered[middle - 1] + ordered[middle]) / 2m
            : ordered[middle];
        return decimal.Round(value, 1, MidpointRounding.AwayFromZero);
    }

    private static int? MedianInt(IEnumerable<decimal> values)
    {
        var median = Median(values);
        return median is null
            ? null
            : (int)Math.Round(median.Value, MidpointRounding.AwayFromZero);
    }

    private static string ToSlot(int hour) => hour < 12 ? "morning" : hour < 18 ? "afternoon" : "evening";

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

    private sealed record EffectiveSale(StatisticsBookMovementProjection Movement, int Quantity);

    private sealed record VolunteerFairProjection(
        AssoEventsId Id,
        string Name,
        DateTimeOffset DateStart,
        decimal? BookRevenue);
}

file static class VolunteerStatisticsBookMovementExtensions
{
    public static string TryGetGenre(
        this StatisticsBookMovementProjection movement,
        IReadOnlyDictionary<string, StatisticsBookProjection> books) =>
        books.TryGetValue(movement.Isbn13.Value, out var book) && !string.IsNullOrWhiteSpace(book.Genre)
            ? book.Genre!
            : "Sans genre";
}
