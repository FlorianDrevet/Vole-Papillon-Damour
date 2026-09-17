using Microsoft.AspNetCore.Http;

namespace Vole_Papillon_Damour.Contracts.RareBooks.Requests;

public sealed record CreateRareBookRequest(
    string Title,
    string? AuthorMention,
    string? Publisher,
    int? PublicationYear,
    string Shelf,
    decimal Price,
    string Condition,
    string? PublicDescription,
    string? Binding,
    string? Dimensions,
    int? PageCount,
    string? ShelfLocation,
    string? PriceSetBy,
    string? Isbn13,
    Guid? ClientGestureId = null);

public sealed record UpdateRareBookRequest(
    string Title,
    string? AuthorMention,
    string? Publisher,
    int? PublicationYear,
    string Shelf,
    decimal Price,
    string Condition,
    string? PublicDescription,
    string? Binding,
    string? Dimensions,
    int? PageCount,
    string? ShelfLocation,
    string? PriceSetBy,
    string? Isbn13,
    string RowVersion);

public sealed record MarkRareBookSoldRequest(
    DateTime OccurredAt,
    Guid? ScanSessionId,
    Guid? AssoEventsId);

public sealed class AddRareBookPhotoRequest
{
    public IFormFile? File { get; set; }
    public string? Caption { get; set; }
}

public sealed record ReorderRareBookPhotosRequest(IReadOnlyList<Guid> PhotoIds);

public sealed record UpdateRareBookPhotoCaptionRequest(string? Caption);
