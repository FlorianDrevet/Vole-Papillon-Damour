namespace Vole_Papillon_Damour.Contracts.Purchases;

public sealed record MyPurchasesResponse(
    IReadOnlyList<PurchasePassageResponse> Passages,
    string? NextCursor);

public sealed record PurchasePassageResponse(
    Guid Id,
    string Reference,
    DateTimeOffset OccurredAt,
    Guid? FairId,
    string? FairLabel,
    int ActiveBookCount,
    IReadOnlyList<PurchaseLineResponse> Lines);

public sealed record PurchaseLineResponse(
    Guid Id,
    string Kind,
    string? Isbn13,
    Guid? RareBookId,
    string Title,
    string? Authors,
    string? Publisher,
    int? PublicationYear,
    string? PhysicalFormat,
    int Quantity,
    string State,
    string? CurrentCoverUrl);
