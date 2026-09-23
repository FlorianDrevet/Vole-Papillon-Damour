namespace Vole_Papillon_Damour.Contracts.MemberSelection;

public sealed record AddSelectionItemRequest(string? Isbn13, Guid? RareBookId);
