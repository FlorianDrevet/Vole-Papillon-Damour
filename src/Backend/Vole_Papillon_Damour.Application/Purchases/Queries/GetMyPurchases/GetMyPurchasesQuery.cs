using ErrorOr;
using MediatR;

namespace Vole_Papillon_Damour.Application.Purchases.Queries.GetMyPurchases;

public sealed record GetMyPurchasesQuery(
    string ExternalId,
    string Email,
    string? FirstName,
    string? LastName,
    string? Cursor,
    int Limit = 10) : IRequest<ErrorOr<MyPurchasesPage>>;

public sealed record MyPurchasesPage(
    IReadOnlyList<PurchasePassageResult> Passages,
    string? NextCursor);

public sealed record PurchasePassageResult(
    Guid Id,
    string Reference,
    DateTimeOffset OccurredAt,
    Guid? FairId,
    string? FairLabel,
    int ActiveBookCount,
    IReadOnlyList<PurchaseLineResult> Lines);

public sealed record PurchaseLineResult(
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
