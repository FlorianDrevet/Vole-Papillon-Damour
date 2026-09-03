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

        public static Error ProviderEventMemberMismatch(object providerEventId) => Error.Conflict(
            code: "Watchlist.ProviderEventMemberMismatch",
            description: $"Provider event is already associated with another member: {providerEventId}.");

        public static Error InvalidBounceTimestamp() => Error.Validation(
            code: "Watchlist.InvalidBounceTimestamp",
            description: "The bounce record timestamp must be expressed in UTC.");
    }
}
