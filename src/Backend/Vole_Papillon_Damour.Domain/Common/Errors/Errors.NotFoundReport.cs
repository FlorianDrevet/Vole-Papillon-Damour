using ErrorOr;

namespace Vole_Papillon_Damour.Domain.Common.Errors;

public static partial class Errors
{
    public static class NotFoundReport
    {
        public static Error InvalidTarget() => Error.Validation(
            code: "NotFoundReport.InvalidTarget",
            description: "The target must identify an edition ISBN-13 or a rare-book identifier.");

        public static Error NotFound(Guid reportId) => Error.NotFound(
            code: "NotFoundReport.NotFound",
            description: $"Not-found report not found: {reportId}.");

        public static Error NotReportable() => Error.Conflict(
            code: "NotFoundReport.NotReportable",
            description: "This selection item is no longer available to report.");

        public static Error DailyLimitReached(int limit) => Error.Custom(
            type: 429,
            code: "NotFoundReport.DailyLimitReached",
            description: $"The daily limit of {limit} not-found reports has been reached.");

        public static Error AlreadyClosed() => Error.Conflict(
            code: "NotFoundReport.AlreadyClosed",
            description: "This not-found report is already closed.");

        public static Error NothingToClose() => Error.Conflict(
            code: "NotFoundReport.NothingToClose",
            description: "There are no open not-found reports for this target.");

        public static Error InvalidFoundQuantity() => Error.Validation(
            code: "NotFoundReport.InvalidFoundQuantity",
            description: "The quantity found must be between zero and the available quantity.");

        public static Error InvalidClosureNote() => Error.Validation(
            code: "NotFoundReport.InvalidClosureNote",
            description: "A closure note is required for this action and cannot exceed 500 characters.");

        public static Error InvalidWithdrawalReason() => Error.Validation(
            code: "NotFoundReport.InvalidWithdrawalReason",
            description: "The withdrawal reason is not recognised.");

        public static Error InvalidAuthor() => Error.Validation(
            code: "NotFoundReport.InvalidAuthor",
            description: "A valid volunteer identifier is required to close reports.");

        public static Error InvalidClosureTimestamp() => Error.Validation(
            code: "NotFoundReport.InvalidClosureTimestamp",
            description: "The closure timestamp must be expressed in UTC.");
    }
}
