using ErrorOr;

namespace Vole_Papillon_Damour.Domain.Common.Errors;
public static partial class Errors
{
    public static class Promotion
    {
        public static Error PromotionNotFound() => Error.NotFound(
            code: "Promotion.NotFound",
            description: "Promotion not found."
        );
        
        public static Error PromotionAlreadyExists() => Error.Conflict(
            code: "Promotion.AlreadyExists",
            description: "Promotion already exists."
        );
    }
}
