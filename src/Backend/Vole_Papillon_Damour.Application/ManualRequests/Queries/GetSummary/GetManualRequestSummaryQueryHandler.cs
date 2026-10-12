using ErrorOr;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Vole_Papillon_Damour.Application.Common.Interfaces.Persistence;
using Vole_Papillon_Damour.Application.Common.Interfaces.Services;
using Vole_Papillon_Damour.Domain.Common.Errors;
using Vole_Papillon_Damour.Domain.ManualRequestAggregate.ValueObjects;

namespace Vole_Papillon_Damour.Application.ManualRequests.Queries.GetSummary;

public sealed class GetManualRequestSummaryQueryHandler(
    IProjectDbContext dbContext,
    IDateTimeProvider dateTimeProvider)
    : IRequestHandler<GetManualRequestSummaryQuery, ErrorOr<ManualRequestSummaryResult>>
{
    public async Task<ErrorOr<ManualRequestSummaryResult>> Handle(
        GetManualRequestSummaryQuery query,
        CancellationToken cancellationToken)
    {
        var now = dateTimeProvider.UtcNow;
        if (now.Kind != DateTimeKind.Utc)
        {
            return Errors.Book.InvalidScanTimestamp();
        }

        var found = dbContext.ManualBookRequests
            .AsNoTracking()
            .Where(request => request.Status == ManualBookRequestStatus.Found);
        var foundCount = await found.CountAsync(cancellationToken);
        var foundOverdueCount = await found.CountAsync(
            request => request.FoundAt < now.AddDays(-14),
            cancellationToken);
        return new ManualRequestSummaryResult(foundCount, foundOverdueCount);
    }
}
