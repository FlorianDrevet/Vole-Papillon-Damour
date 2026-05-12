using ErrorOr;
using Vole_Papillon_Damour.Domain.ActualityAggregate.ValueObjects;

namespace Vole_Papillon_Damour.Domain.Common.Errors;

public static partial class Errors
{
    public static class BingoCard
    {
        public static Error CannotOpenImage() => Error.Failure(
            code: "BingoCard.CannotOpenImage",
            description: "Cannot open image"
        );
    }
}