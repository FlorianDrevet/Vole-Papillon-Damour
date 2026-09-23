namespace Vole_Papillon_Damour.Application.MemberSelection.Common;

public sealed record SelectionItemAddedResult(Guid Id, bool AlreadyPresent);

public sealed record SelectionTarget(
    Vole_Papillon_Damour.Domain.BookAggregate.ValueObjects.Isbn13? Isbn13,
    Vole_Papillon_Damour.Domain.RareBookAggregate.ValueObjects.RareBookId? RareBookId);
