using ErrorOr;

namespace Vole_Papillon_Damour.Domain.Common.Errors;

public static partial class Errors
{
    public static class Favorites
    {
        public static Error InvalidIsbn(string? value) => Error.Validation(
            code: "Favorites.InvalidIsbn",
            description: $"The ISBN-13 is invalid: {value ?? "(null)"}.");

        public static Error LimitReached(int maximumItems) => Error.Conflict(
            code: "Favorites.LimitReached",
            description: $"A member cannot keep more than {maximumItems} favorite books.");
    }
}
