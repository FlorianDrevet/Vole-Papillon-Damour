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

        public static Error InvalidSaleQuantity() => Error.Validation(
            code: "Book.InvalidSaleQuantity",
            description: "The sale quantity must be positive.");

        public static Error InvalidSaleTimestamp() => Error.Validation(
            code: "Book.InvalidSaleTimestamp",
            description: "The sale timestamp must be expressed in UTC.");

        public static Error ClientGestureAlreadyUsed(object clientGestureId) => Error.Conflict(
            code: "Book.ClientGestureAlreadyUsed",
            description: $"Client gesture identifier is already used: {clientGestureId}.");

        public static Error SaleNotFound(object movementId) => Error.NotFound(
            code: "Book.SaleNotFound",
            description: $"Sale movement not found: {movementId}.");

        public static Error NotASaleMovement(object movementId) => Error.Validation(
            code: "Book.NotASaleMovement",
            description: $"Movement is not a sale: {movementId}.");

        public static Error SaleAlreadyVoided(object movementId) => Error.Conflict(
            code: "Book.SaleAlreadyVoided",
            description: $"Sale has already been voided: {movementId}.");

        public static Error SaleCancellationOutsideOpenFair() => Error.Conflict(
            code: "Book.SaleCancellationOutsideOpenFair",
            description: "A sale can only be cancelled while its book fair is open.");

        public static Error NotFound(object isbn13) => Error.NotFound(
            code: "Book.NotFound",
            description: $"Book not found: {isbn13}.");

        public static Error InvalidCorrectionQuantity() => Error.Validation(
            code: "Book.InvalidCorrectionQuantity",
            description: "The corrected available quantity cannot be negative.");

        public static Error InvalidCorrectionNote() => Error.Validation(
            code: "Book.InvalidCorrectionNote",
            description: "A correction note is required and cannot exceed 500 characters.");

        public static Error InvalidAssociationSettings() => Error.Validation(
            code: "Book.InvalidAssociationSettings",
            description: "Association settings contain an invalid threshold or delay.");

        public static Error FairNotFound(object fairId) => Error.NotFound(
            code: "Book.FairNotFound",
            description: $"Book fair not found: {fairId}.");

        public static Error TargetFairMustBeBooks() => Error.Validation(
            code: "Book.TargetFairMustBeBooks",
            description: "The target event must be a books fair.");

        public static Error ScanSessionAlreadyReassigned(object scanSessionId) => Error.Conflict(
            code: "Book.ScanSessionAlreadyReassigned",
            description: $"Scan session has already been reassigned: {scanSessionId}.");

        public static Error ScanSessionMustBeClosed(object scanSessionId) => Error.Conflict(
            code: "Book.ScanSessionMustBeClosed",
            description: $"Scan session must be closed before reassignment: {scanSessionId}.");

        public static Error ScanSessionAlreadyInTargetMode(object scanSessionId) => Error.Validation(
            code: "Book.ScanSessionAlreadyInTargetMode",
            description: $"Scan session is already in the requested mode: {scanSessionId}.");

        public static Error AnnouncementAlreadyReleased(string isbn13) => Error.Conflict(
            code: "Book.AnnouncementAlreadyReleased",
            description: $"The announcement for ISBN {isbn13} can no longer be cancelled.");
    }
}
