using Vole_Papillon_Damour.Domain.BookAggregate.ValueObjects;

namespace Vole_Papillon_Damour.Application.Books.Commands.UploadBookCover;

public sealed record UploadBookCoverResult(
    string Isbn13,
    Uri CoverUrl,
    BookMetadataStatus MetadataStatus,
    string ManuallyEditedFields,
    DateTime UpdatedAt,
    bool Changed);
