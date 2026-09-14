using ErrorOr;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Vole_Papillon_Damour.Application.Books.Common;
using Vole_Papillon_Damour.Application.Common.Interfaces.Persistence;
using Vole_Papillon_Damour.Application.Common.Interfaces.Services;
using Vole_Papillon_Damour.Domain.BookMovementAggregate.ValueObjects;
using Vole_Papillon_Damour.Domain.EventsAggregate.ValueObjects;

namespace Vole_Papillon_Damour.Application.Books.Queries.Admin;

public sealed class GetAdminFairsEvolutionQueryHandler(
    IProjectDbContext dbContext,
    IDateTimeProvider dateTimeProvider)
    : IRequestHandler<GetAdminFairsEvolutionQuery, ErrorOr<AdminFairsEvolutionResult>>
{
    private static readonly string[] Seasons = {"Printemps", "Été", "Automne", "Hiver"};

    public async Task<ErrorOr<AdminFairsEvolutionResult>> Handle(
        GetAdminFairsEvolutionQuery query,
        CancellationToken cancellationToken)
    {
        var generatedAt = dateTimeProvider.UtcNow;
        if (query.From is not null && query.To is not null && query.From >= query.To)
        {
            return Error.Validation("Book.InvalidPeriod", "The fairs evolution period must be valid instants.");
        }

        var fairs = await dbContext.AssoEvents
            .AsNoTracking()
            .Where(candidate => candidate.EventsType == new EventsType(EventsType.EventsTypeEnum.Books) &&
                                 !candidate.IsCancelled)
            .ToListAsync(cancellationToken);
        // SQLite cannot translate DateTimeOffset comparisons consistently; compare dates in memory.
        fairs = fairs
            .Where(fair => IsInPeriod(fair.DateStart, query.From, query.To))
            .OrderBy(fair => fair.DateStart)
            .ToList();

        var fairIds = fairs.Select(fair => fair.Id).ToArray();
        var sales = await dbContext.BookMovements
            .AsNoTracking()
            .Where(movement =>
                movement.Type == BookMovementType.Sale &&
                movement.AssoEventsId != null &&
                fairIds.Contains(movement.AssoEventsId!))
            .ToListAsync(cancellationToken);

        var entries = new List<AdminFairEvolutionEntryResult>(fairs.Count);
        AdminFairEvolutionEntryResult? previous = null;
        foreach (var fair in fairs)
        {
            var soldQuantity = sales
                .Where(sale => sale.AssoEventsId == fair.Id)
                .Sum(sale => Math.Abs(sale.Quantity));
            var revenue = fair.BookRevenue;
            var averageBasket = revenue is not null && soldQuantity > 0
                ? revenue.Value / soldQuantity
                : (decimal?)null;
            var variationPercent = previous is null || previous.SoldQuantity == 0
                ? (decimal?)null
                : decimal.Round(
                    (soldQuantity - previous.SoldQuantity) * 100m / previous.SoldQuantity,
                    1,
                    MidpointRounding.AwayFromZero);
            var daysOpen = fair.DateEnd is null
                ? (int?)null
                : Math.Max(1, (int)Math.Ceiling((fair.DateEnd.Value - fair.DateStart).TotalDays) + 1);

            var entry = new AdminFairEvolutionEntryResult(
                fair.Id.Value,
                fair.Name,
                fair.DateStart,
                fair.DateEnd,
                soldQuantity,
                revenue,
                averageBasket,
                variationPercent,
                daysOpen);
            entries.Add(entry);
            previous = entry;
        }

        var totalSoldQuantity = entries.Sum(entry => entry.SoldQuantity);
        var totalRevenue = entries.Any(entry => entry.Revenue is not null)
            ? entries.Sum(entry => entry.Revenue ?? 0m)
            : (decimal?)null;
        var averageBasketOverall = totalRevenue is not null && totalSoldQuantity > 0
            ? totalRevenue.Value / totalSoldQuantity
            : (decimal?)null;
        var growthSinceFirstPercent = entries.Count >= 2 && entries[0].SoldQuantity > 0
            ? decimal.Round(
                (entries[^1].SoldQuantity - entries[0].SoldQuantity) * 100m / entries[0].SoldQuantity,
                1,
                MidpointRounding.AwayFromZero)
            : (decimal?)null;

        return new AdminFairsEvolutionResult(
            new DateTimeOffset(generatedAt, TimeSpan.Zero),
            query.From,
            query.To,
            entries.Count,
            totalSoldQuantity,
            totalRevenue,
            averageBasketOverall,
            growthSinceFirstPercent,
            BuildSeasons(entries),
            entries);
    }

    private static IReadOnlyList<AdminFairEvolutionSeasonResult> BuildSeasons(
        IReadOnlyCollection<AdminFairEvolutionEntryResult> entries)
    {
        return Seasons
            .Select(season =>
            {
                var matching = entries.Where(entry => GetSeason(entry.DateStart.Month) == season).ToArray();
                return new AdminFairEvolutionSeasonResult(
                    season,
                    matching.Length == 0
                        ? null
                        : decimal.Round((decimal)matching.Average(entry => entry.SoldQuantity), 1, MidpointRounding.AwayFromZero),
                    matching.Length);
            })
            .ToArray();
    }

    private static string GetSeason(int month) => month switch
    {
        3 or 4 or 5 => "Printemps",
        6 or 7 or 8 => "Été",
        9 or 10 or 11 => "Automne",
        _ => "Hiver",
    };

    private static bool IsInPeriod(DateTimeOffset value, DateTimeOffset? from, DateTimeOffset? to) =>
        (from is null || value >= from.Value) && (to is null || value < to.Value);
}
