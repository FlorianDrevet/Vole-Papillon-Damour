using ErrorOr;
using MediatR;
using Vole_Papillon_Damour.Application.MemberCards.Queries.GetMyCard;

namespace Vole_Papillon_Damour.Application.MemberCards.Commands.RotateMyCard;

public sealed record RotateMyCardCommand(
    Guid ExternalId,
    string Email,
    string? FirstName,
    string? LastName) : IRequest<ErrorOr<MyCardResult>>;
