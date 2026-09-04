using ErrorOr;
using MediatR;
using Vole_Papillon_Damour.Application.Books.Common;

namespace Vole_Papillon_Damour.Application.Books.Queries.GetPublicWork;

public sealed record GetPublicWorkQuery(string WorkId)
    : IRequest<ErrorOr<PublicCatalogWorkResult>>;
