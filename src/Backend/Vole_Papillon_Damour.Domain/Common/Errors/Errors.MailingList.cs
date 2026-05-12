using ErrorOr;

namespace Vole_Papillon_Damour.Domain.Common.Errors;

public static partial class Errors
{
    public static class MailingList
    {
        public static Error EmailAlreadyExists(string email) => Error.Conflict(
            code: "MailingList.Email.AlreadyExists",
            description: "Email already exists in the mailing list: " + email
        );
        
        public static Error ErrorWhileAddingEmail(string email) => Error.Failure(
            code: "MailingList.Email.ErrorWhileAdding",
            description: "Error while adding email to the mailing list: " + email
        );
        
        public static Error EmailDoesNotExist(string email) => Error.NotFound(
            code: "MailingList.Email.NotFound",
            description: "Email does not exist in the mailing list: " + email
        );
    }
}