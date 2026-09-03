using ErrorOr;
using MediatR;
using Vole_Papillon_Damour.Application.Books.Common;

namespace Vole_Papillon_Damour.Application.Books.Queries.GetCatalogDelta;

public sealed record GetCatalogDeltaQuery(DateTime? Since)
    : IRequest<ErrorOr<ScanCatalogDeltaResult>>;
