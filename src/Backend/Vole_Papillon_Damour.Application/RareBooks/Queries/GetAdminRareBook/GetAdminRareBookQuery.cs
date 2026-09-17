using ErrorOr;
using MediatR;
using Vole_Papillon_Damour.Application.RareBooks.Common;
using Vole_Papillon_Damour.Domain.RareBookAggregate.ValueObjects;

namespace Vole_Papillon_Damour.Application.RareBooks.Queries.GetAdminRareBook;

public sealed record GetAdminRareBookQuery(
    RareBookId RareBookId) : IRequest<ErrorOr<RareBookResult>>;
