using ErrorOr;
using MediatR;

namespace Vole_Papillon_Damour.Application.ManualRequests.Queries.GetScanCandidate;

public sealed record GetManualRequestScanCandidateQuery(string Isbn13)
    : IRequest<ErrorOr<ManualRequestScanCandidateResult?>>;

public sealed record ManualRequestScanCandidateResult(
    Guid RequestId,
    string FirstName,
    string LastName,
    string Phone);
