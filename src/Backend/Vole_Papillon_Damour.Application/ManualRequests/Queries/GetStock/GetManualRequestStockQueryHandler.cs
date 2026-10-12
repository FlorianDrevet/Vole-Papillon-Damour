using ErrorOr;
using MediatR;
using Vole_Papillon_Damour.Application.Common.Interfaces.Persistence;
using Vole_Papillon_Damour.Application.ManualRequests.Common;
using Vole_Papillon_Damour.Domain.BookAggregate.ValueObjects;
using Vole_Papillon_Damour.Domain.Common.Errors;
using Vole_Papillon_Damour.Domain.ManualRequestAggregate;
using Vole_Papillon_Damour.Domain.ManualRequestAggregate.ValueObjects;

namespace Vole_Papillon_Damour.Application.ManualRequests.Queries.GetStock;

public sealed class GetManualRequestStockQueryHandler(IProjectDbContext dbContext)
    : IRequestHandler<GetManualRequestStockQuery, ErrorOr<ManualRequestStockSnapshot>>
{
    public Task<ErrorOr<ManualRequestStockSnapshot>> Handle(
        GetManualRequestStockQuery query,
        CancellationToken cancellationToken)
    {
        if (query.Scope == ManualBookRequestScope.Work)
        {
            var workId = string.IsNullOrWhiteSpace(query.WorkId) ? null : query.WorkId.Trim();
            if (workId is null || workId.Length > ManualBookRequest.WorkIdMaxLength)
            {
                return Task.FromResult<ErrorOr<ManualRequestStockSnapshot>>(
                    Errors.ManualRequest.InvalidTarget());
            }

            return GetAsync(ManualBookRequestScope.Work, workId, null, cancellationToken);
        }

        if (query.Scope == ManualBookRequestScope.Edition &&
            Isbn13.TryCreate(query.Isbn13, out var isbn13))
        {
            return GetAsync(ManualBookRequestScope.Edition, null, isbn13, cancellationToken);
        }

        return Task.FromResult<ErrorOr<ManualRequestStockSnapshot>>(
            Errors.ManualRequest.InvalidTarget());
    }

    private async Task<ErrorOr<ManualRequestStockSnapshot>> GetAsync(
        ManualBookRequestScope scope,
        string? workId,
        Isbn13? isbn13,
        CancellationToken cancellationToken)
    {
        var snapshot = await ManualRequestStock.GetAsync(
            dbContext,
            scope,
            workId,
            isbn13,
            cancellationToken);
        return snapshot;
    }
}
