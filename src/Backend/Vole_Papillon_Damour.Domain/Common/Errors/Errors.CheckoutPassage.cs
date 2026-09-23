using ErrorOr;

namespace Vole_Papillon_Damour.Domain.Common.Errors;

public static partial class Errors
{
    public static class CheckoutPassage
    {
        public static Error NotFound(Guid id) => Error.NotFound(
            code: "CheckoutPassage.NotFound",
            description: $"Checkout passage not found: {id}.");

        public static Error AlreadyAssociatedToAnotherMember() => Error.Conflict(
            code: "CheckoutPassage.AlreadyAssociatedToAnotherMember",
            description: "A validated checkout passage cannot be moved to another member.");

        public static Error InvalidId() => Error.Validation(
            code: "CheckoutPassage.InvalidId",
            description: "A valid checkout passage identifier is required.");
    }
}
