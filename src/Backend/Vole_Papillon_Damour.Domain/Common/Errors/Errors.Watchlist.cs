using ErrorOr;

namespace Vole_Papillon_Damour.Domain.Common.Errors;

public static partial class Errors
{
    public static class Watchlist
    {
        public static Error NotFound(object userId) => Error.NotFound(
            code: "Watchlist.NotFound",
            description: $"Watchlist not found for member: {userId}.");
    }
}
