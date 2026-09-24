using ErrorOr;
using MediatR;

namespace Vole_Papillon_Damour.Application.MemberCards.Queries.ResolveMemberCard;

public sealed record ResolveMemberCardQuery(string Credential) : IRequest<ErrorOr<MemberCardConfirmation>>;
