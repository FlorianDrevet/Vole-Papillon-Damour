namespace Vole_Papillon_Damour.Contracts.MemberSelection;

public sealed record MySelectionResponse(
    DateTimeOffset GeneratedAt,
    NextFairSummaryResponse? NextFair,
    IReadOnlyList<MySelectionItemResponse> Items);

public sealed record NextFairSummaryResponse(Guid Id, DateTimeOffset StartsAt);

public sealed record MySelectionItemResponse(
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
    string Availability,
    DateTimeOffset AvailabilityCheckedAt,
    string Status,
    DateTimeOffset AddedAt,
    DateTimeOffset? PurchasedAt,
    NotFoundReportSummaryResponse? NotFoundReport);

public sealed record NotFoundReportSummaryResponse(
    Guid Id,
    string Status,
    DateTimeOffset ReportedAt,
    DateTimeOffset? ClosedAt);

public sealed record NotFoundReportCreatedResponse(
    Guid ReportId,
    DateTimeOffset ReportedAt,
    bool AlreadyOpen);
