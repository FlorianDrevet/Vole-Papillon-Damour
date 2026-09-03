namespace Vole_Papillon_Damour.Domain.BookAggregate.ValueObjects;

public sealed record BookMetadataPatch(
    string? Title,
    string? Authors,
    string? Publisher,
    int? PublicationYear,
    string? PhysicalFormat,
    string? Language,
    string? Genre,
    string? CoverBlobRef,
    IReadOnlyCollection<BookMetadataField> Fields,
    string? WorkId = null);
