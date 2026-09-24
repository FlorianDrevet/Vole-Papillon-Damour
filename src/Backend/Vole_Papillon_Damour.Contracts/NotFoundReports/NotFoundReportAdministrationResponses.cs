namespace Vole_Papillon_Damour.Contracts.NotFoundReports;

public sealed record NotFoundReportQueueResponse(
    DateTimeOffset GeneratedAt,
    int OpenTargetCount,
    int OpenReportCount,
    int OverdueTargetCount,
    int Page,
    int PageSize,
    int TotalCount,
    IReadOnlyList<NotFoundReportTargetResponse> Items);

public sealed record NotFoundReportTargetResponse(
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
    IReadOnlyList<NotFoundReportCommentResponse> Comments);

public sealed record NotFoundReportCommentResponse(
    string Text,
    string? Location,
    DateTimeOffset ReportedAt);

public sealed record ClosedNotFoundReportsResponse(
    DateTimeOffset GeneratedAt,
    int Page,
    int PageSize,
    int TotalCount,
    IReadOnlyList<ClosedNotFoundReportResponse> Items);

public sealed record ClosedNotFoundReportResponse(
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

public sealed record NotFoundReportSummaryResponse(
    int OpenTargetCount,
    int OverdueTargetCount,
    int OpenReportCount,
    DateTimeOffset? FirstReportedAt,
    NotFoundReportCommentResponse? LatestComment);

public sealed record NotFoundClosureResponse(
    int ClosedReportCount,
    int? WithdrawnQuantity,
    int? QuantityAvailable,
    Guid? MovementId);
