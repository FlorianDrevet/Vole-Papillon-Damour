using ErrorOr;

namespace Vole_Papillon_Damour.Domain.Common.Errors;

public static partial class Errors
{
    public static class ManualRequest
    {
        public static Error RequesterNotFound(Guid id) => Error.NotFound(
            code: "ManualRequest.RequesterNotFound",
            description: $"Manual requester not found: {id}.");

        public static Error RequestNotFound(Guid id) => Error.NotFound(
            code: "ManualRequest.RequestNotFound",
            description: $"Manual book request not found: {id}.");

        public static Error ConsentRequired() => Error.Validation(
            code: "ManualRequest.ConsentRequired",
            description: "The requester's consent is required.");

        public static Error InvalidName() => Error.Validation(
            code: "ManualRequest.InvalidName",
            description: "A valid first and last name are required.");

        public static Error InvalidPhone() => Error.Validation(
            code: "ManualRequest.InvalidPhone",
            description: "A valid phone number is required.");

        public static Error InvalidTarget() => Error.Validation(
            code: "ManualRequest.InvalidTarget",
            description: "The request target must identify a work or an ISBN-13 edition.");

        public static Error DuplicateRequest() => Error.Conflict(
            code: "ManualRequest.DuplicateRequest",
            description: "An open request already exists for this target and requester.");

        public static Error LimitReached(int limit) => Error.Conflict(
            code: "ManualRequest.LimitReached",
            description: $"The maximum of {limit} open requests has been reached.");

        public static Error InvalidTransition() => Error.Conflict(
            code: "ManualRequest.InvalidTransition",
            description: "The manual book request cannot make that state transition.");

        public static Error NothingInStock() => Error.Conflict(
            code: "ManualRequest.NothingInStock",
            description: "No available or announced copy can be picked for this request.");

        public static Error RequesterErased() => Error.Conflict(
            code: "ManualRequest.RequesterErased",
            description: "The requester's contact details have been erased.");

        public static Error InvalidAuthor() => Error.Validation(
            code: "ManualRequest.InvalidAuthor",
            description: "A valid administrator or volunteer identifier is required.");
    }
}
