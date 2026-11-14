using ErrorOr;
using MediatR;

namespace Vole_Papillon_Damour.Application.Recommendations.Commands.RevokeFairDigestConsent;

public sealed record RevokeFairDigestConsentCommand(Guid MemberId) : IRequest<ErrorOr<Success>>;
