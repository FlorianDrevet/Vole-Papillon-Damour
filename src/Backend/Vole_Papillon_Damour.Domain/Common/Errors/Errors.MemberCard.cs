using ErrorOr;

namespace Vole_Papillon_Damour.Domain.Common.Errors;

public static partial class Errors
{
    public static class MemberCard
    {
        public static Error NotRecognised() => Error.NotFound(
            code: "MemberCard.NotRecognised",
            description: "The member card credential was not recognised.");

        public static Error Revoked() => Error.Validation(
            code: "MemberCard.Revoked",
            description: "The member card is no longer active.");

        public static Error InvalidRecoveryCode() => Error.Validation(
            code: "MemberCard.InvalidRecoveryCode",
            description: "The recovery code format is not recognised.");
    }
}
