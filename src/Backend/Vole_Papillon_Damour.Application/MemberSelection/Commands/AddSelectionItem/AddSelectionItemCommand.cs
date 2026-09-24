using ErrorOr;
using MediatR;
using Vole_Papillon_Damour.Application.MemberSelection.Common;

namespace Vole_Papillon_Damour.Application.MemberSelection.Commands.AddSelectionItem;

public sealed record AddSelectionItemCommand(
    Guid ExternalId,
    string Email,
    string? FirstName,
    string? LastName,
    string? Isbn13,
    Guid? RareBookId) : IRequest<ErrorOr<SelectionItemAddedResult>>;
