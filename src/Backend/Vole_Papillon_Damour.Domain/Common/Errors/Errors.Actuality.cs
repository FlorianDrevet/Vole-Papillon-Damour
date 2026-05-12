using ErrorOr;
using Vole_Papillon_Damour.Domain.ActualityAggregate.ValueObjects;

namespace Vole_Papillon_Damour.Domain.Common.Errors;

public static partial class Errors
{
    public static class Actuality
    {
        public static Error ActualityNotFound(ActualityId id) => Error.NotFound(
            code: "Actuality.NotFound",
            description: "Actuality not found with id: " + id.Value
        );
    }
}