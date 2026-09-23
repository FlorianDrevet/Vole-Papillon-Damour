namespace Vole_Papillon_Damour.Contracts.MemberSelection;

public sealed record MergeSelectionRequest(IReadOnlyList<MergeSelectionEntryRequest> Entries);

public sealed record MergeSelectionEntryRequest(
    string? Isbn13,
    Guid? RareBookId,
    DateTimeOffset AddedAt);
