using ErrorOr;
using MediatR;
using Vole_Papillon_Damour.Application.Books.Common;

namespace Vole_Papillon_Damour.Application.Books.Queries.GetPublicNextBookFair;

public sealed record GetPublicNextBookFairQuery
    : IRequest<ErrorOr<PublicBookFairResult>>;
