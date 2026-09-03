using ErrorOr;

namespace Vole_Papillon_Damour.Domain.Common.Errors;

public static partial class Errors
{
    public static class Book
    {
        public static Error InvalidIsbn(string input) => Error.Validation(
            code: "Book.InvalidIsbn",
            description: $"'{input}' is not a valid ISBN-10 or ISBN-13.");

        public static Error MetadataNotFound(string isbn13) => Error.NotFound(
            code: "Book.MetadataNotFound",
            description: $"No bibliographic metadata was found for ISBN {isbn13}.");
    }
}
