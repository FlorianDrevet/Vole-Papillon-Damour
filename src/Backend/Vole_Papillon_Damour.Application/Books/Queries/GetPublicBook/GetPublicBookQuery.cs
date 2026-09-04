using ErrorOr;
using MediatR;
using Vole_Papillon_Damour.Application.Books.Common;

namespace Vole_Papillon_Damour.Application.Books.Queries.GetPublicBook;

public sealed record GetPublicBookQuery(string Isbn13)
    : IRequest<ErrorOr<PublicCatalogBookResult>>;
