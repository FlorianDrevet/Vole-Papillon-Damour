using ErrorOr;
using MediatR;
using Vole_Papillon_Damour.Application.Books.Common;
using Vole_Papillon_Damour.Domain.BookAggregate.ValueObjects;
using Vole_Papillon_Damour.Domain.UserAggregate.ValueObjects;

namespace Vole_Papillon_Damour.Application.Books.Commands.Admin;

public sealed record AddBookCommand(
    string Isbn,
    int QuantityAvailable,
    string Note,
    UserId AddedBy,
    string? Title = null,
    string? Authors = null,
    string? Publisher = null,
    int? PublicationYear = null,
    string? PhysicalFormat = null,
    string? Language = null,
    string? Genre = null,
    string? CoverBlobRef = null,
    string? WorkId = null,
    IReadOnlyCollection<BookMetadataField>? Fields = null) : IRequest<ErrorOr<AdminBookOperationResult>>;
