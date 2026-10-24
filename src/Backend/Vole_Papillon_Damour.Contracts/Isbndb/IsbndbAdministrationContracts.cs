namespace Vole_Papillon_Damour.Contracts.Isbndb;

public sealed record IsbndbStatusResponse(
    int DailyLimit,
    int DailyRemaining,
    DateTimeOffset? ObservedAt,
    DateTimeOffset? ResetsAt,
    string Status,
    DateTimeOffset? ExhaustedUntil,
    IReadOnlyList<IsbndbUsageTodayResponse> UsageToday,
    IsbndbPendingCatchUpResponse PendingCatchUp,
    int AbsentToComplete,
    IsbndbLastRunResponse? LastRun,
    IReadOnlyList<IsbndbUnmatchedSubjectResponse> UnmatchedSubjects);

public sealed record IsbndbUsageTodayResponse(string Usage, int Credits);
public sealed record IsbndbPendingCatchUpResponse(int Total, int InStock, int BeforeMigration);
public sealed record IsbndbLastRunResponse(
    DateTimeOffset StartedAt,
    DateTimeOffset? EndedAt,
    int Filled,
    int Absent,
    int Remaining,
    string? StopReason);
public sealed record IsbndbUnmatchedSubjectResponse(string Subject, int Count);

public sealed record IsbndbAbsentBooksPageResponse(
    IReadOnlyList<IsbndbAbsentBookResponse> Items,
    int TotalCount,
    int Page,
    int PageSize);

public sealed record IsbndbAbsentBookResponse(
    string Isbn13,
    string? Title,
    string? Authors,
    string? CoverUrl,
    string? FallbackSource,
    int QuantityAvailable,
    IReadOnlyList<string> MissingFields,
    string? Summary,
    string? Genre,
    string? Publisher,
    int? PublicationYear,
    int? Pages,
    string? Edition,
    IReadOnlyList<string> Genres,
    IReadOnlyList<string> Subjects,
    DateTimeOffset FirstSeenAt,
    DateTimeOffset? IsbndbCheckedAt,
    DateTimeOffset? IsbndbCompletedAt);

public sealed record SetIsbndbCompletionRequest(bool Completed);
public sealed record IsbndbCompletionResponse(
    string Isbn13,
    bool IsCompleted,
    DateTimeOffset? CompletedAt,
    Guid? CompletedBy,
    bool Changed);
public sealed record IsbndbRecheckResponse(string Isbn13, string Outcome, DateTimeOffset? ResetsAt = null);
