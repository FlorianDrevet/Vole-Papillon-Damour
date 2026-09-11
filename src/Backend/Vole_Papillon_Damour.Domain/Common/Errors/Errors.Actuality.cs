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

        public static Error CannotPublish(ActualityId id) => Error.Validation(
            code: "Actuality.CannotPublish",
            description: "Actuality cannot be published until its title and article are filled: " + id.Value
        );
    }
}
