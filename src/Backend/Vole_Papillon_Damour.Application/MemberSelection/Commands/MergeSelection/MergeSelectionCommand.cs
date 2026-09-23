using ErrorOr;
using MediatR;
using Vole_Papillon_Damour.Application.MemberSelection.Common;

namespace Vole_Papillon_Damour.Application.MemberSelection.Commands.MergeSelection;

public sealed record MergeSelectionEntry(string? Isbn13, Guid? RareBookId, DateTime AddedAt);

public sealed record MergeSelectionCommand(
    Guid ExternalId,
    string Email,
    string? FirstName,
    string? LastName,
    IReadOnlyList<MergeSelectionEntry> Entries) : IRequest<ErrorOr<MergeSelectionResult>>;
