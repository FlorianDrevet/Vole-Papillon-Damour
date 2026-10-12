using ErrorOr;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Vole_Papillon_Damour.Application.Common.Interfaces.Persistence;
using Vole_Papillon_Damour.Application.Common.Interfaces.Services;
using Vole_Papillon_Damour.Application.ManualRequests.Common;
using Vole_Papillon_Damour.Domain.BookAggregate.ValueObjects;
using Vole_Papillon_Damour.Domain.Common.Errors;

namespace Vole_Papillon_Damour.Application.ManualRequests.Queries.GetScanCandidate;

public sealed class GetManualRequestScanCandidateQueryHandler(
    IProjectDbContext dbContext,
    IBibliographicMetadataResolver? metadataResolver = null)
    : IRequestHandler<GetManualRequestScanCandidateQuery, ErrorOr<ManualRequestScanCandidateResult?>>
{
    public async Task<ErrorOr<ManualRequestScanCandidateResult?>> Handle(
        GetManualRequestScanCandidateQuery query,
        CancellationToken cancellationToken)
    {
        if (!Isbn13.TryCreate(query.Isbn13, out var isbn13))
        {
            return Errors.Book.InvalidIsbn(query.Isbn13);
        }

        var workId = await ManualRequestMatcher.ResolveWorkIdAsync(
            dbContext, metadataResolver, isbn13, cancellationToken);
        var request = await ManualRequestMatcher.FindOldestSearchingAsync(
            dbContext, isbn13, workId, cancellationToken);
        if (request is null)
        {
            return (ManualRequestScanCandidateResult?)null;
        }

        var requester = await dbContext.ManualRequesters
            .AsNoTracking()
            .SingleOrDefaultAsync(candidate => candidate.Id == request.RequesterId, cancellationToken);
        if (requester is null || requester.IsErased ||
            string.IsNullOrWhiteSpace(requester.FirstName) ||
            string.IsNullOrWhiteSpace(requester.LastName) ||
            string.IsNullOrWhiteSpace(requester.Phone))
        {
            return (ManualRequestScanCandidateResult?)null;
        }

        return new ManualRequestScanCandidateResult(
            request.Id,
            requester.FirstName,
            requester.LastName,
            requester.Phone);
    }
}
