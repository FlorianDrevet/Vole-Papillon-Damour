using Vole_Papillon_Damour.Domain.RareBookAggregate.ValueObjects;

namespace Vole_Papillon_Damour.Application.RareBooks.Common;

public enum RareBookSortOrder
{
    PriceDescending,
    PriceAscending,
    Recent
}

public enum RareBookAdminAvailability
{
    All,
    Available,
    Sold
}

public sealed record RareBookPhotoResult(
    Guid Id,
    Uri BlobUri,
    string BlobName,
    string? Caption,
    int Position,
    string ContentType,
    long SizeBytes,
    DateTimeOffset UploadedAt,
    Guid UploadedBy);

public sealed record RareBookResult(
    Guid Id,
    string Slug,
    string? Isbn13,
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
    string Status,
    bool IsSold,
    DateTimeOffset? SoldAt,
    Guid? SoldAtFairId,
    Guid? SoldInSessionId,
    string? PriceSetBy,
    DateTimeOffset CreatedAt,
    Guid CreatedBy,
    DateTimeOffset UpdatedAt,
    Guid UpdatedBy,
    byte[] RowVersion,
    IReadOnlyList<RareBookPhotoResult> Photos);

public sealed record RareBookPublishResult(
    RareBookResult RareBook,
    bool Changed,
    IReadOnlyList<string> Warnings);

public sealed record RareBookPageResult(
    DateTimeOffset GeneratedAt,
    IReadOnlyList<RareBookResult> Books,
    int TotalCount,
    int Page,
    int PageSize);

public sealed record PublicRareBookResult(
    Guid Id,
    string Slug,
    string? Isbn13,
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
    string Status,
    bool IsSold,
    DateTimeOffset? SoldAt,
    IReadOnlyList<RareBookPhotoResult> Photos);

public sealed record PublicRareBookPageResult(
    DateTimeOffset GeneratedAt,
    IReadOnlyList<PublicRareBookResult> Books,
    int TotalCount,
    int Page,
    int PageSize,
    IReadOnlyList<RareBookShelfCountResult> Shelves);

public sealed record RareBookShelfCountResult(
    string Label,
    int Count);

public sealed record PublicRareBookDetailResult(
    PublicRareBookResult RareBook,
    IReadOnlyList<PublicRareBookResult> RelatedBooks);

public sealed record CashRareBookResult(
    Guid Id,
    string Title,
    string? AuthorMention,
    string? Isbn13,
    decimal Price,
    string Shelf,
    string Condition,
    Uri? Thumbnail,
    string Slug);

public sealed record RareBookCashSearchResult(
    DateTimeOffset GeneratedAt,
    IReadOnlyList<CashRareBookResult> Books,
    int TotalCount,
    int Page,
    int PageSize);

internal static class RareBookResultProjector
{
    public static RareBookResult ToResult(Domain.RareBookAggregate.RareBook book)
    {
        return new RareBookResult(
            book.Id.Value,
            book.Slug.Value,
            book.Isbn13?.Value,
            book.Title,
            book.AuthorMention,
            book.Publisher,
            book.PublicationYear,
            book.Shelf.Value,
            book.Price,
            book.Condition.Value.ToString(),
            book.PublicDescription,
            book.Binding,
            book.Dimensions,
            book.PageCount,
            book.ShelfLocation,
            book.Status.ToString(),
            book.IsSold,
            ToOffset(book.SoldAt),
            book.SoldAtFairId?.Value,
            book.SoldInSessionId?.Value,
            book.PriceSetBy,
            ToOffset(book.CreatedAt),
            book.CreatedBy.Value,
            ToOffset(book.UpdatedAt),
            book.UpdatedBy.Value,
            book.RowVersion.ToArray(),
            book.Photos.OrderBy(photo => photo.Position).Select(ToResult).ToArray());
    }

    public static PublicRareBookResult ToPublicResult(Domain.RareBookAggregate.RareBook book)
    {
        return new PublicRareBookResult(
            book.Id.Value,
            book.Slug.Value,
            book.Isbn13?.Value,
            book.Title,
            book.AuthorMention,
            book.Publisher,
            book.PublicationYear,
            book.Shelf.Value,
            book.Price,
            book.Condition.Value.ToString(),
            book.PublicDescription,
            book.Binding,
            book.Dimensions,
            book.PageCount,
            book.Status.ToString(),
            book.IsSold,
            ToOffset(book.SoldAt),
            book.Photos.OrderBy(photo => photo.Position).Select(ToResult).ToArray());
    }

    public static CashRareBookResult ToCashResult(Domain.RareBookAggregate.RareBook book)
    {
        var photo = book.Photos.OrderBy(candidate => candidate.Position).FirstOrDefault();
        return new CashRareBookResult(
            book.Id.Value,
            book.Title,
            book.AuthorMention,
            book.Isbn13?.Value,
            book.Price,
            book.Shelf.Value,
            book.Condition.Value.ToString(),
            photo?.BlobUri,
            book.Slug.Value);
    }

    private static RareBookPhotoResult ToResult(
        Domain.RareBookAggregate.Entities.RareBookPhoto photo)
    {
        return new RareBookPhotoResult(
            photo.Id.Value,
            photo.BlobUri,
            photo.BlobName,
            photo.Caption,
            photo.Position,
            photo.ContentType,
            photo.SizeBytes,
            ToOffset(photo.UploadedAt),
            photo.UploadedBy.Value);
    }

    private static DateTimeOffset ToOffset(DateTime value) =>
        new(value, TimeSpan.Zero);

    private static DateTimeOffset? ToOffset(DateTime? value) =>
        value is { } dateTime ? ToOffset(dateTime) : null;
}
