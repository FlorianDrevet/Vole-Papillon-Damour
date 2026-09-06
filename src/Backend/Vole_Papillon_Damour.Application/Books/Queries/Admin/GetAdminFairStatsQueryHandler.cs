using ErrorOr;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Vole_Papillon_Damour.Application.Books.Common;
using Vole_Papillon_Damour.Application.Common.Interfaces.Persistence;
using Vole_Papillon_Damour.Domain.BookAggregate.ValueObjects;
using Vole_Papillon_Damour.Domain.BookMovementAggregate.ValueObjects;
using Vole_Papillon_Damour.Domain.Common.Errors;
using Vole_Papillon_Damour.Domain.EventsAggregate.ValueObjects;

namespace Vole_Papillon_Damour.Application.Books.Queries.Admin;

public sealed class GetAdminFairStatsQueryHandler(IProjectDbContext dbContext)
    : IRequestHandler<GetAdminFairStatsQuery, ErrorOr<AdminFairStatsResult>>
{
    public async Task<ErrorOr<AdminFairStatsResult>> Handle(
        GetAdminFairStatsQuery query,
        CancellationToken cancellationToken)
    {
        if (query.FairId == Guid.Empty)
        {
            return Errors.Book.FairNotFound(query.FairId);
        }

        var fair = await dbContext.AssoEvents.AsNoTracking().SingleOrDefaultAsync(
            candidate => candidate.Id == AssoEventsId.Create(query.FairId),
            cancellationToken);
        if (fair is null)
        {
            return Errors.Book.FairNotFound(query.FairId);
        }

        if (fair.EventsType?.Value != EventsType.EventsTypeEnum.Books)
        {
            return Errors.Book.TargetFairMustBeBooks();
        }

        var sales = await dbContext.BookMovements
            .AsNoTracking()
            .Where(movement =>
                movement.AssoEventsId == fair.Id &&
                movement.Type == BookMovementType.Sale)
            .ToListAsync(cancellationToken);
        var soldIsbns = sales.Select(sale => sale.Isbn13).Distinct().ToArray();
        var books = await dbContext.Books
            .AsNoTracking()
            .Where(book => soldIsbns.Contains(book.Id))
            .ToDictionaryAsync(book => book.Id, cancellationToken);

        var soldQuantity = sales.Sum(sale => Math.Abs(sale.Quantity));
        var salesByGenre = sales
            .GroupBy(sale => books.TryGetValue(sale.Isbn13, out var book) ? book.Genre : null,
                StringComparer.OrdinalIgnoreCase)
            .Select(group => new AdminGenreSalesResult(
                string.IsNullOrWhiteSpace(group.Key) ? null : group.Key,
                group.Sum(sale => Math.Abs(sale.Quantity))))
            .OrderByDescending(item => item.Quantity)
            .ThenBy(item => item.Genre)
            .ToArray();
        var topBooks = sales
            .GroupBy(sale => sale.Isbn13)
            .Select(group =>
            {
                books.TryGetValue(group.Key, out var book);
                return new AdminTopBookResult(
                    group.Key.Value,
                    book?.Title,
                    book?.Authors,
                    book?.Genre,
                    group.Sum(sale => Math.Abs(sale.Quantity)));
            })
            .OrderByDescending(item => item.Quantity)
            .ThenBy(item => item.Title)
            .Take(20)
            .ToArray();
        var dailySales = sales
            .GroupBy(sale => DateOnly.FromDateTime(sale.OccurredAt))
            .Select(group => new AdminDailySalesResult(
                group.Key,
                group.Sum(sale => Math.Abs(sale.Quantity))))
            .OrderBy(item => item.Day)
            .ToArray();

        var previousFairs = await dbContext.AssoEvents
            .AsNoTracking()
            .Where(candidate =>
                candidate.EventsType.Value == EventsType.EventsTypeEnum.Books &&
                candidate.DateStart < fair.DateStart &&
                !candidate.IsCancelled)
            .OrderByDescending(candidate => candidate.DateStart)
            .Take(5)
            .ToListAsync(cancellationToken);
        var previousFairIds = previousFairs.Select(candidate => candidate.Id).ToArray();
        var previousSales = await dbContext.BookMovements
            .AsNoTracking()
            .Where(movement =>
                movement.Type == BookMovementType.Sale &&
                movement.AssoEventsId != null &&
                previousFairIds.Contains(movement.AssoEventsId!))
            .ToListAsync(cancellationToken);
        var comparisons = previousFairs
            .Select(previous => new AdminFairComparisonResult(
                previous.Id.Value,
                previous.Name,
                previous.DateStart,
                previousSales
                    .Where(sale => sale.AssoEventsId == previous.Id)
                    .Sum(sale => Math.Abs(sale.Quantity)),
                previous.BookRevenue))
            .ToArray();

        var revenue = fair.BookRevenue;
        return new AdminFairStatsResult(
            new AdminFairResult(
                fair.Id.Value,
                fair.Name,
                fair.DateStart,
                fair.DateEnd,
                fair.IsCancelled,
                fair.BookRevenue),
            soldQuantity,
            soldIsbns.Length,
            revenue,
            revenue is not null && soldQuantity > 0
                ? revenue.Value / soldQuantity
                : null,
            salesByGenre,
            topBooks,
            dailySales,
            comparisons);
    }
}
