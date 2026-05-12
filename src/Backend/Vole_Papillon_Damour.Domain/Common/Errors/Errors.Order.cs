using ErrorOr;
using Vole_Papillon_Damour.Domain.OrderAggregate.ValueObjects;

namespace Vole_Papillon_Damour.Domain.Common.Errors;
public static partial class Errors
{
    public static class Order
    {
        public static Error OrderNotFound(OrderId orderId) => Error.NotFound(
            code: "Order.NotFound",
            description: "Order not found: " + orderId.Value
        );

        public static Error OrderedProductNotFound() => Error.NotFound(
            code: "Order.Product.NotFound",
            description: "Trying to create an order with unknown Product"
        );
        
        public static Error StatusUnknown() => Error.Validation(
            code: "Order.Status.Invalid",
            description: "status is unknown"
        );
    }
}
