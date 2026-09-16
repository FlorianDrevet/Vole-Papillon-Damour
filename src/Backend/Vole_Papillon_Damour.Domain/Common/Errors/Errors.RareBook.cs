using ErrorOr;

namespace Vole_Papillon_Damour.Domain.Common.Errors;

public static partial class Errors
{
    public static class RareBook
    {
        public static Error NotFound(object rareBookId) => Error.NotFound(
            code: "RareBook.NotFound",
            description: $"Rare book not found: {rareBookId}.");

        public static Error PhotoNotFound(object photoId) => Error.NotFound(
            code: "RareBook.PhotoNotFound",
            description: $"Rare book photo not found: {photoId}.");

        public static Error InvalidId() => Error.Validation(
            code: "RareBook.InvalidId",
            description: "A valid rare book identifier is required.");

        public static Error InvalidPhotoId() => Error.Validation(
            code: "RareBook.InvalidPhotoId",
            description: "A valid rare book photo identifier is required.");

        public static Error InvalidUser() => Error.Validation(
            code: "RareBook.InvalidUser",
            description: "A valid user identifier is required.");

        public static Error InvalidTimestamp() => Error.Validation(
            code: "RareBook.InvalidTimestamp",
            description: "The timestamp must be expressed in UTC.");

        public static Error InvalidIsbn(string input) => Error.Validation(
            code: "RareBook.InvalidIsbn",
            description: $"'{input}' is not a valid ISBN-10 or ISBN-13.");

        public static Error InvalidData(string description) => Error.Validation(
            code: "RareBook.InvalidData",
            description: description);

        public static Error DuplicateIsbn(string isbn13) => Error.Conflict(
            code: "RareBook.DuplicateIsbn",
            description: $"A rare book already uses ISBN {isbn13}.");

        public static Error SlugConflict(string slug) => Error.Conflict(
            code: "RareBook.SlugConflict",
            description: $"A rare book already uses slug '{slug}'.");

        public static Error ConcurrencyConflict(object rareBookId) => Error.Conflict(
            code: "RareBook.ConcurrencyConflict",
            description: $"The rare book was changed by another user: {rareBookId}.");

        public static Error InvalidPaging() => Error.Validation(
            code: "RareBook.InvalidPaging",
            description: "Page must be positive and page size must be between 1 and 60.");

        public static Error InvalidStatus() => Error.Validation(
            code: "RareBook.InvalidStatus",
            description: "The rare book status is not supported.");

        public static Error InvalidSort() => Error.Validation(
            code: "RareBook.InvalidSort",
            description: "The rare book sort is not supported.");

        public static Error CannotPublish(object rareBookId) => Error.Validation(
            code: "RareBook.CannotPublish",
            description: $"The rare book cannot be published until its title and positive price are valid: {rareBookId}.");

        public static Error CannotSellDraft(object rareBookId) => Error.Conflict(
            code: "RareBook.CannotSellDraft",
            description: $"A draft rare book cannot be marked sold: {rareBookId}.");

        public static Error AlreadySold(object rareBookId) => Error.Conflict(
            code: "RareBook.AlreadySold",
            description: $"The rare book is already sold: {rareBookId}.");

        public static Error RestoreWindowExpired(object rareBookId) => Error.Conflict(
            code: "RareBook.RestoreWindowExpired",
            description: $"The rare book can no longer be restored automatically: {rareBookId}.");

        public static Error InvalidPhotoType(string contentType) => Error.Validation(
            code: "RareBook.InvalidPhotoType",
            description: $"Photo type '{contentType}' is not supported. Use JPEG, WebP, or PNG.");

        public static Error InvalidPhotoSize(long sizeBytes) => Error.Validation(
            code: "RareBook.InvalidPhotoSize",
            description: $"Photo size must be greater than zero and no larger than 8388608 bytes (received {sizeBytes}).");

        public static Error InvalidPhotoOrder() => Error.Validation(
            code: "RareBook.InvalidPhotoOrder",
            description: "Photo order must contain every existing photo exactly once.");

        public static Error EmptyPhotoBlobName() => Error.Unexpected(
            code: "RareBook.EmptyPhotoBlobName",
            description: "The photo blob service did not return a usable blob name.");
    }
}
