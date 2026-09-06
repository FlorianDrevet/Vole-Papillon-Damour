using ErrorOr;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Vole_Papillon_Damour.Application.Books.Common;
using Vole_Papillon_Damour.Application.Common.Interfaces.Persistence;
using Vole_Papillon_Damour.Application.Common.Interfaces.Services;
using Vole_Papillon_Damour.Domain.Common.Errors;
using Vole_Papillon_Damour.Domain.EventsAggregate.ValueObjects;

namespace Vole_Papillon_Damour.Application.Books.Queries.Admin;

public sealed class GetAdminBookFairsQueryHandler(
    IProjectDbContext dbContext,
    IDateTimeProvider dateTimeProvider)
    : IRequestHandler<GetAdminBookFairsQuery, ErrorOr<AdminFairPageResult>>
{
    public async Task<ErrorOr<AdminFairPageResult>> Handle(
        GetAdminBookFairsQuery query,
        CancellationToken cancellationToken)
    {
        if (query.Page <= 0 || query.PageSize is <= 0 or > 200)
        {
            return Errors.Book.InvalidAdminPage();
        }

        var generatedAt = dateTimeProvider.UtcNow;
        if (generatedAt.Kind != DateTimeKind.Utc)
        {
            return Error.Validation("Book.InvalidClock", "The administration clock must be expressed in UTC.");
        }

        var fairs = await dbContext.AssoEvents
            .AsNoTracking()
            .Where(fair => fair.EventsType.Value == EventsType.EventsTypeEnum.Books &&
                          (query.IncludeCancelled || !fair.IsCancelled))
            .OrderByDescending(fair => fair.DateStart)
            .ToListAsync(cancellationToken);
        var totalCount = fairs.Count;
        var page = fairs.Skip((query.Page - 1) * query.PageSize).Take(query.PageSize);
        return new AdminFairPageResult(
            new DateTimeOffset(generatedAt, TimeSpan.Zero),
            page.Select(fair => new AdminFairResult(
                    fair.Id.Value,
                    fair.Name,
                    fair.DateStart,
                    fair.DateEnd,
                    fair.IsCancelled,
                    fair.BookRevenue))
                .ToArray(),
            totalCount,
            query.Page,
            query.PageSize);
    }
}
