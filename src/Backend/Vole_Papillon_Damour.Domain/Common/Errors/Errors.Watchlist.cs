using ErrorOr;

namespace Vole_Papillon_Damour.Domain.Common.Errors;

public static partial class Errors
{
    public static class Watchlist
    {
        public static Error NotFound(object userId) => Error.NotFound(
            code: "Watchlist.NotFound",
            description: $"Watchlist not found for member: {userId}.");

        public static Error InvalidProviderEventId() => Error.Validation(
            code: "Watchlist.InvalidProviderEventId",
            description: "A provider event identifier is required and must not exceed 128 characters.");

        public static Error InvalidRecipient() => Error.Validation(
            code: "Watchlist.InvalidRecipient",
            description: "A recipient email address is required and must not exceed 320 characters.");

        public static Error InvalidScope() => Error.Validation(
            code: "Watchlist.InvalidScope",
            description: "A watchlist item scope must be Work or Edition.");

        public static Error InvalidWorkTarget() => Error.Validation(
            code: "Watchlist.InvalidWorkTarget",
            description: "A work watchlist item requires a work identifier of at most 64 characters.");

        public static Error InvalidEditionTarget() => Error.Validation(
            code: "Watchlist.InvalidEditionTarget",
            description: "An edition watchlist item requires a valid ISBN-13 and no work identifier.");

        public static Error DuplicateItem() => Error.Conflict(
            code: "Watchlist.DuplicateItem",
            description: "This book is already present in the watchlist.");

        public static Error LimitReached(int maximumItems) => Error.Conflict(
            code: "Watchlist.LimitReached",
            description: $"A watchlist cannot contain more than {maximumItems} items.");

        public static Error ItemNotFound(object itemId) => Error.NotFound(
            code: "Watchlist.ItemNotFound",
            description: $"Watchlist item not found: {itemId}.");

        public static Error ProviderEventMemberMismatch(object providerEventId) => Error.Conflict(
            code: "Watchlist.ProviderEventMemberMismatch",
            description: $"Provider event is already associated with another member: {providerEventId}.");

        public static Error InvalidBounceTimestamp() => Error.Validation(
            code: "Watchlist.InvalidBounceTimestamp",
            description: "The bounce record timestamp must be expressed in UTC.");
    }
}
