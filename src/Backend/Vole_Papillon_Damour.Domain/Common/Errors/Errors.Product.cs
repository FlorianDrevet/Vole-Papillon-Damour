using ErrorOr;

namespace Vole_Papillon_Damour.Domain.Common.Errors;
public static partial class Errors
{
    public static class Product
    {
        public static Error ProductNotFound() => Error.NotFound(
            code: "Product.NotFound",
            description: "Product not found."
        );

        public static Error InvalidBookProductOrder() => Error.Validation(
            code: "Product.InvalidBookProductOrder",
            description: "The order must include every book product exactly once."
        );
    }
}
