using Vole_Papillon_Damour.Application.NotFoundReports.Queries.GetNotFoundReportQueue;

namespace Vole_Papillon_Damour.Application.NotFoundReports.Queries.GetNotFoundReportSummary;

public sealed record NotFoundReportSummaryResult(
    int OpenTargetCount,
    int OverdueTargetCount,
    int OpenReportCount,
    DateTimeOffset? FirstReportedAt,
    NotFoundReportCommentResult? LatestComment);
