using Vole_Papillon_Damour.Domain.BookAggregate.ValueObjects;

namespace Vole_Papillon_Damour.Application.Books.Common;

public sealed record UpdateBookMetadataResult(
    string Isbn13,
    BookMetadataStatus MetadataStatus,
    BookMetadataSource MetadataSource,
    string? ManuallyEditedFields,
    DateTime UpdatedAt,
    bool Changed);
