using ErrorOr;
using MediatR;
using Vole_Papillon_Damour.Application.Orders.Common;

namespace Vole_Papillon_Damour.Application.Orders.Queries.GetAllOrdersQuery;

public record GetAllOrdersQuery(
    
) : IRequest<ErrorOr<List<OrderResult>>>;