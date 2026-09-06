using ErrorOr;
using MediatR;
using Vole_Papillon_Damour.Application.Books.Common;
using Vole_Papillon_Damour.Domain.BookAggregate.ValueObjects;
using Vole_Papillon_Damour.Domain.UserAggregate.ValueObjects;

namespace Vole_Papillon_Damour.Application.Books.Commands.UpdateBookMetadata;

public sealed record UpdateBookMetadataCommand(
    string Isbn,
    string? Title,
    string? Authors,
    string? Publisher,
    int? PublicationYear,
    string? PhysicalFormat,
    string? Language,
    string? Genre,
    string? CoverUrl,
    IReadOnlyCollection<BookMetadataField> Fields,
    UserId UpdatedBy,
    string? WorkId = null) : IRequest<ErrorOr<UpdateBookMetadataResult>>;
