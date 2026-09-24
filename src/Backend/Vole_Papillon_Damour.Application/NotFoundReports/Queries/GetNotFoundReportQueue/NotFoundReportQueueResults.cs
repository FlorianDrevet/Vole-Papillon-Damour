namespace Vole_Papillon_Damour.Application.NotFoundReports.Queries.GetNotFoundReportQueue;

public sealed record NotFoundReportQueueResult(
    DateTimeOffset GeneratedAt,
    int OpenTargetCount,
    int OpenReportCount,
    int OverdueTargetCount,
    int Page,
    int PageSize,
    int TotalCount,
    IReadOnlyList<NotFoundReportTargetResult> Items);

public sealed record NotFoundReportTargetResult(
    string Kind,
    string? Isbn13,
    Guid? RareBookId,
    string Title,
    string? Authors,
    string? Publisher,
    int? PublicationYear,
    string? CoverUrl,
    string? Genre,
    int QuantityAvailable,
    int ReportCount,
    int MemberCount,
    DateTimeOffset FirstReportedAt,
    DateTimeOffset LastReportedAt,
    bool Overdue,
    IReadOnlyList<NotFoundReportCommentResult> Comments);

public sealed record NotFoundReportCommentResult(
    string Text,
    string? Location,
    DateTimeOffset ReportedAt);
