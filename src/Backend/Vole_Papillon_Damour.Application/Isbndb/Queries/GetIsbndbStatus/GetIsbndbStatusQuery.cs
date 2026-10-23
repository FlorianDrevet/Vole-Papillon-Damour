using MediatR;

namespace Vole_Papillon_Damour.Application.Isbndb.Queries.GetIsbndbStatus;

public sealed record GetIsbndbStatusQuery : IRequest<IsbndbStatusResult>;

public sealed record IsbndbStatusResult(
    int DailyLimit,
    int DailyRemaining,
    DateTimeOffset? ObservedAt,
    DateTimeOffset? ResetsAt,
    string Status,
    DateTimeOffset? ExhaustedUntil,
    IReadOnlyList<IsbndbUsageTodayResult> UsageToday,
    IsbndbPendingCatchUpResult PendingCatchUp,
    int AbsentToComplete,
    IsbndbLastRunResult? LastRun,
    IReadOnlyList<IsbndbUnmatchedSubjectResult> UnmatchedSubjects);

public sealed record IsbndbUsageTodayResult(string Usage, int Credits);
public sealed record IsbndbPendingCatchUpResult(int Total, int InStock, int BeforeMigration);
public sealed record IsbndbLastRunResult(
    DateTimeOffset StartedAt,
    DateTimeOffset? EndedAt,
    int Filled,
    int Absent,
    int Remaining,
    string? StopReason);
public sealed record IsbndbUnmatchedSubjectResult(string Subject, int Count);
