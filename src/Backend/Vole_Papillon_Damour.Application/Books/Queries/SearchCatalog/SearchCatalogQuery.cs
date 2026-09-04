using ErrorOr;
using MediatR;
using Vole_Papillon_Damour.Application.Books.Common;

namespace Vole_Papillon_Damour.Application.Books.Queries.SearchCatalog;

public sealed record SearchCatalogQuery(
    string? Search,
    string? Genre,
    PublicCatalogAvailabilityFilter Availability,
    bool RareOnly,
    PublicCatalogSortOrder Sort,
    int Page,
    int PageSize) : IRequest<ErrorOr<PublicCatalogSearchResult>>;
