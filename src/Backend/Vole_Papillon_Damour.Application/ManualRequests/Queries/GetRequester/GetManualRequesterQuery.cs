using ErrorOr;
using MediatR;

namespace Vole_Papillon_Damour.Application.ManualRequests.Queries.GetRequester;

public sealed record GetManualRequesterQuery(Guid RequesterId)
    : IRequest<ErrorOr<ManualRequesterDetailResult>>;
