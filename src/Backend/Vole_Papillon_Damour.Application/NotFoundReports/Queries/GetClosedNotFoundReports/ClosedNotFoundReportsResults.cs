namespace Vole_Papillon_Damour.Application.NotFoundReports.Queries.GetClosedNotFoundReports;

public sealed record ClosedNotFoundReportsResult(
    DateTimeOffset GeneratedAt,
    int Page,
    int PageSize,
    int TotalCount,
    IReadOnlyList<ClosedNotFoundReportResult> Items);

public sealed record ClosedNotFoundReportResult(
    DateTimeOffset ClosedAt,
    string Kind,
    string? Isbn13,
    Guid? RareBookId,
    string Title,
    string Outcome,
    int ReportCount,
    int? WithdrawnQuantity,
    string? ClosedByName,
    string? Note);
