using ErrorOr;
using MediatR;
using Vole_Papillon_Damour.Application.RareBooks.Common;

namespace Vole_Papillon_Damour.Application.RareBooks.Queries.GetPublicRareBookByIdentifier;

public sealed record GetPublicRareBookByIdentifierQuery(
    string Identifier) : IRequest<ErrorOr<PublicRareBookDetailResult>>;
