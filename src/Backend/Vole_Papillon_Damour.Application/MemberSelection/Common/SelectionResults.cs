using Vole_Papillon_Damour.Domain.BookAggregate.ValueObjects;
using Vole_Papillon_Damour.Domain.MemberSelectionAggregate.ValueObjects;
using Vole_Papillon_Damour.Domain.RareBookAggregate.ValueObjects;

namespace Vole_Papillon_Damour.Application.MemberSelection.Common;

public sealed record SelectionItemAddedResult(Guid Id, bool AlreadyPresent);

public sealed record MergeSelectionResult(int Added, int AlreadyPresent, IReadOnlyList<string> Rejected);

public sealed record SelectionTarget(Isbn13? Isbn13, RareBookId? RareBookId);

public enum SelectionAvailability
{
    Available,
    Announced,
    OutOfStock,
    RareSold,
    Unavailable
}

public sealed record MySelectionResult(
    DateTimeOffset GeneratedAt,
    NextFairSummary? NextFair,
    IReadOnlyList<SelectionItemResult> Items);

public sealed record NextFairSummary(Guid Id, DateTimeOffset StartsAt);

public sealed record SelectionItemResult(
    Guid Id,
    string Kind,
    string? Isbn13,
    Guid? RareBookId,
    string? RareBookSlug,
    string Title,
    string? Authors,
    string? Publisher,
    int? PublicationYear,
    string? PhysicalFormat,
    string? CoverUrl,
    SelectionAvailability Availability,
    DateTimeOffset AvailabilityCheckedAt,
    MemberSelectionStatus Status,
    DateTimeOffset AddedAt,
    DateTimeOffset? PurchasedAt);
