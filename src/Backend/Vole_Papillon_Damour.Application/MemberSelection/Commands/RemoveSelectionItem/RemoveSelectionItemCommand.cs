using ErrorOr;
using MediatR;

namespace Vole_Papillon_Damour.Application.MemberSelection.Commands.RemoveSelectionItem;

public sealed record RemoveSelectionItemCommand(
    Guid ExternalId,
    string Email,
    string? FirstName,
    string? LastName,
    Guid ItemId) : IRequest<ErrorOr<Deleted>>;
