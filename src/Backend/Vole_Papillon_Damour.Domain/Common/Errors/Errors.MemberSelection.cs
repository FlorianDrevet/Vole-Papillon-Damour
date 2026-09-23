using ErrorOr;

namespace Vole_Papillon_Damour.Domain.Common.Errors;

public static partial class Errors
{
    public static class MemberSelection
    {
        public static Error NotFound(Guid itemId) => Error.NotFound(
            code: "MemberSelection.NotFound",
            description: $"Selection item not found: {itemId}.");

        public static Error InvalidTarget() => Error.Validation(
            code: "MemberSelection.InvalidTarget",
            description: "Provide exactly one ISBN-13 or one rare book identifier.");

        public static Error InvalidStatus() => Error.Validation(
            code: "MemberSelection.InvalidStatus",
            description: "The selection status is not recognised.");

        public static Error NotInCatalog(string reference) => Error.Validation(
            code: "MemberSelection.NotInCatalog",
            description: $"Only a record visible in the public catalogue can be selected: {reference}.");

        public static Error TooManyItems(int max) => Error.Validation(
            code: "MemberSelection.TooManyItems",
            description: $"A selection cannot contain more than {max} items.");
    }
}
