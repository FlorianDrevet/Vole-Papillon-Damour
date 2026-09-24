using ErrorOr;
using MediatR;

namespace Vole_Papillon_Damour.Application.MemberCards.Queries.GetMyCard;

public sealed record GetMyCardQuery(
    Guid ExternalId,
    string Email,
    string? FirstName,
    string? LastName) : IRequest<ErrorOr<MyCardResult>>;
