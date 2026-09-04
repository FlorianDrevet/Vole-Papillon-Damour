using ErrorOr;
using MediatR;
using Vole_Papillon_Damour.Application.Books.Common;

namespace Vole_Papillon_Damour.Application.Books.Queries.GetDeadStock;

public sealed record GetDeadStockQuery(
    int MinAgeMonths,
    int MinQuantity) : IRequest<ErrorOr<DeadStockResult>>
{
    public const int DefaultMinAgeMonths = 6;
    public const int DefaultMinQuantity = 3;
}
