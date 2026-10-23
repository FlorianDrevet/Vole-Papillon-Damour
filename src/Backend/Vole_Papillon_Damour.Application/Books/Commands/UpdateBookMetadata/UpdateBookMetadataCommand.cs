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
    string? WorkId = null,
    string? Summary = null,
    int? Pages = null,
    string? Edition = null,
    IReadOnlyCollection<string>? Genres = null) : IRequest<ErrorOr<UpdateBookMetadataResult>>;
