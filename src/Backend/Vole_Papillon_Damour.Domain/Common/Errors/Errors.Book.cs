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

        public static Error ScanSessionNotFound(object scanSessionId) => Error.NotFound(
            code: "Book.ScanSessionNotFound",
            description: $"Scan session not found: {scanSessionId}.");

        public static Error ScanSessionClosed(object scanSessionId) => Error.Conflict(
            code: "Book.ScanSessionClosed",
            description: $"Scan session is already closed: {scanSessionId}.");

        public static Error InvalidScanTimestamp() => Error.Validation(
            code: "Book.InvalidScanTimestamp",
            description: "The scan timestamp must be expressed in UTC.");

        public static Error RedirectTargetNotFound(string isbn13) => Error.Unexpected(
            code: "Book.RedirectTargetNotFound",
            description: $"The canonical target for ISBN {isbn13} does not exist.");

        public static Error ActiveScanSessionExists(object volunteerId) => Error.Conflict(
            code: "Book.ActiveScanSessionExists",
            description: $"Volunteer already has an active scan session: {volunteerId}.");

        public static Error InvalidScanMode() => Error.Validation(
            code: "Book.InvalidScanMode",
            description: "The scan mode is not supported.");

        public static Error TargetFairOnlyForNextFair() => Error.Validation(
            code: "Book.TargetFairOnlyForNextFair",
            description: "A fair can only be targeted by a NextFair scan session.");
    }
}
