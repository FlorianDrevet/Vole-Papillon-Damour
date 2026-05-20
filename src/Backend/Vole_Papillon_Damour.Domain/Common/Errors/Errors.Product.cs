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
    }
}
