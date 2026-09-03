using ErrorOr;
using MediatR;
using Vole_Papillon_Damour.Application.Books.Common;
using Vole_Papillon_Damour.Domain.BookAggregate.ValueObjects;

namespace Vole_Papillon_Damour.Application.Books.Queries.GetBookMetadata;

public sealed record GetBookMetadataQuery(Isbn13 Isbn13) : IRequest<ErrorOr<BookMetadataResult>>;
