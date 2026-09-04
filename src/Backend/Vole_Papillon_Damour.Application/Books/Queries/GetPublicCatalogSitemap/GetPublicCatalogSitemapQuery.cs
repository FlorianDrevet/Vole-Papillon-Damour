using ErrorOr;
using MediatR;
using Vole_Papillon_Damour.Application.Books.Common;

namespace Vole_Papillon_Damour.Application.Books.Queries.GetPublicCatalogSitemap;

public sealed record GetPublicCatalogSitemapQuery
    : IRequest<ErrorOr<PublicCatalogSitemapResult>>;
