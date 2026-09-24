namespace Vole_Papillon_Damour.Application.NotFoundReports.Common;

public sealed record NotFoundReportCreatedResult(Guid ReportId, DateTimeOffset ReportedAt, bool AlreadyOpen);
