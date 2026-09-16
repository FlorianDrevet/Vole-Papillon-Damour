using ErrorOr;
using MediatR;
using Vole_Papillon_Damour.Application.RareBooks.Common;

namespace Vole_Papillon_Damour.Application.RareBooks.Queries.GetPublicRareBookBySlug;

public sealed record GetPublicRareBookBySlugQuery(
    string Slug) : IRequest<ErrorOr<PublicRareBookDetailResult>>;
