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

namespace Vole_Papillon_Damour.Application.Books.Queries.Admin;

public sealed class GetAdminCatalogueFlowStatsQueryHandler(
    IProjectDbContext dbContext,
    IDateTimeProvider dateTimeProvider)
    : IRequestHandler<GetAdminCatalogueFlowStatsQuery, ErrorOr<AdminCatalogueFlowStatsResult>>
{
    // Time-to-sell is approximated at ISBN level (first "kept" movement to each sale
    // movement) since BookMovement carries a signed quantity, not per-copy identifiers.
    private static readonly (string Label, int MaxDays)[] TimeToSellBuckets =
    {
        ("< 1 mois", 30),
        ("1-3 mois", 90),
        ("3-6 mois", 180),
        ("6-12 mois", 365),
        ("> 12 mois", int.MaxValue),
    };

    public async Task<ErrorOr<AdminCatalogueFlowStatsResult>> Handle(
        GetAdminCatalogueFlowStatsQuery query,
        CancellationToken cancellationToken)
    {
        var generatedAt = dateTimeProvider.UtcNow;
        var from = query.From?.UtcDateTime;
        var to = query.To?.UtcDateTime;
        if (from is not null && to is not null && from >= to)
        {
            return Error.Validation("Book.InvalidPeriod", "The catalogue flow statistics period must be valid UTC instants.");
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
        var sessionsById = sessions.ToDictionary(session => session.Id, session => session);
        var scannedCount = sessions.Sum(session => session.ScannedCount);

        var relevantMovements = await dbContext.BookMovements
            .AsNoTracking()
            .Where(movement =>
                movement.Type == BookMovementType.DirectEntry ||
                movement.Type == BookMovementType.AnnouncementEntry ||
                movement.Type == BookMovementType.Sale ||
                movement.Type == BookMovementType.Correction)
            .ToListAsync(cancellationToken);
        var movements = relevantMovements
            .Where(movement => IsInPeriod(movement.OccurredAt, from, to))
            .Where(movement => IsInSelectedFair(movement, sessionsById, selectedFair?.Id))
            .ToArray();

        var books = await dbContext.Books.AsNoTracking().ToDictionaryAsync(book => book.Id, cancellationToken);

        var keptMovements = movements
            .Where(movement => movement.Type is BookMovementType.DirectEntry or BookMovementType.AnnouncementEntry)
            .ToArray();
        var sales = BuildEffectiveSales(movements);

        var keptCount = keptMovements.Sum(movement => Math.Abs(movement.Quantity));
        var soldCount = sales.Sum(sale => sale.Quantity);

        var keptByIsbn = keptMovements
            .GroupBy(movement => movement.Isbn13)
            .ToDictionary(group => group.Key, group => group.Sum(movement => Math.Abs(movement.Quantity)));
        var soldByIsbn = sales
            .GroupBy(sale => sale.Movement.Isbn13)
            .ToDictionary(group => group.Key, group => group.Sum(sale => sale.Quantity));
        var dormantCount = keptByIsbn.Sum(item => Math.Max(0, item.Value - soldByIsbn.GetValueOrDefault(item.Key)));
        var dormantOverYearCount = keptMovements
            .Where(movement => movement.OccurredAt <= generatedAt.AddYears(-1))
            .GroupBy(movement => movement.Isbn13)
            .Sum(group => Math.Max(0, group.Sum(movement => Math.Abs(movement.Quantity)) - soldByIsbn.GetValueOrDefault(group.Key)));

        var funnel = new AdminCatalogueFunnelResult(
            scannedCount,
            keptCount,
            Percent(keptCount, scannedCount),
            soldCount,
            Percent(soldCount, keptCount),
            dormantCount,
            dormantOverYearCount);

        return new AdminCatalogueFlowStatsResult(
            new DateTimeOffset(generatedAt, TimeSpan.Zero),
            query.From,
            query.To,
            funnel,
            BuildGenreFlow(keptMovements, sales, books),
            BuildTimeToSellDistribution(keptMovements, sales));
    }

    private static IReadOnlyList<AdminCatalogueGenreFlowResult> BuildGenreFlow(
        IReadOnlyCollection<BookMovement> keptMovements,
        IReadOnlyCollection<EffectiveSale> sales,
        IReadOnlyDictionary<Isbn13, Book> books)
    {
        var keptByGenre = keptMovements
            .GroupBy(movement => GetGenre(movement.Isbn13, books), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                group => group.Key,
                group => group.Sum(movement => Math.Abs(movement.Quantity)),
                StringComparer.OrdinalIgnoreCase);
        var soldByGenre = sales
            .GroupBy(sale => GetGenre(sale.Movement.Isbn13, books), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.Sum(sale => sale.Quantity), StringComparer.OrdinalIgnoreCase);

        return keptByGenre
            .Select(item => new AdminCatalogueGenreFlowResult(
                item.Key,
                item.Value,
                soldByGenre.GetValueOrDefault(item.Key),
                Percent(soldByGenre.GetValueOrDefault(item.Key), item.Value)))
            .OrderByDescending(item => item.KeptQuantity)
            .ThenBy(item => item.Genre, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static IReadOnlyList<AdminCatalogueTimeToSellBucketResult> BuildTimeToSellDistribution(
        IReadOnlyCollection<BookMovement> keptMovements,
        IReadOnlyCollection<EffectiveSale> sales)
    {
        var firstKeptAtByIsbn = keptMovements
            .GroupBy(movement => movement.Isbn13)
            .ToDictionary(group => group.Key, group => group.Min(movement => movement.OccurredAt));

        var delays = sales
            .Where(sale => firstKeptAtByIsbn.ContainsKey(sale.Movement.Isbn13))
            .SelectMany(sale => Enumerable.Repeat(
                (sale.Movement.OccurredAt - firstKeptAtByIsbn[sale.Movement.Isbn13]).TotalDays,
                sale.Quantity))
            .ToArray();

        var counts = TimeToSellBuckets
            .Select((bucket, index) =>
            {
                var minDays = index == 0 ? 0 : TimeToSellBuckets[index - 1].MaxDays;
                return (bucket.Label, Count: delays.Count(days => days >= minDays && days < bucket.MaxDays));
            })
            .ToArray();
        var total = counts.Sum(item => item.Count);

        return counts
            .Select(item => new AdminCatalogueTimeToSellBucketResult(item.Label, item.Count, Percent(item.Count, total)))
            .ToArray();
    }

    private static string GetGenre(Isbn13 isbn, IReadOnlyDictionary<Isbn13, Book> books) =>
        books.TryGetValue(isbn, out var book) && !string.IsNullOrWhiteSpace(book.Genre)
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
        if (fairId is null || movement.AssoEventsId == fairId)
        {
            return true;
        }

        return movement.ScanSessionId is { } sessionId &&
               sessions.TryGetValue(sessionId, out var session) &&
               session.TargetAssoEventsId == fairId;
    }

    private static decimal? Percent(int numerator, int denominator) =>
        denominator == 0 ? null : decimal.Round(numerator * 100m / denominator, 1, MidpointRounding.AwayFromZero);

    private sealed record EffectiveSale(BookMovement Movement, int Quantity);
}
