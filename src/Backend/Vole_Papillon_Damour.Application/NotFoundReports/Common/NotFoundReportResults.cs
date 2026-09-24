namespace Vole_Papillon_Damour.Application.NotFoundReports.Common;

public sealed record NotFoundReportCreatedResult(Guid ReportId, DateTimeOffset ReportedAt, bool AlreadyOpen);

public sealed record NotFoundReportTargetRef(string Kind, string Reference);

public sealed record NotFoundClosureResult(
    int ClosedReportCount,
    int? WithdrawnQuantity,
    int? QuantityAvailable,
    Guid? MovementId);
