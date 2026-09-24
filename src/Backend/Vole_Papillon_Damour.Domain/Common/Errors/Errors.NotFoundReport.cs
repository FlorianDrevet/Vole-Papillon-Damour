using ErrorOr;

namespace Vole_Papillon_Damour.Domain.Common.Errors;

public static partial class Errors
{
    public static class NotFoundReport
    {
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
    }
}
