using Vole_Papillon_Damour.Domain.Common;
using Vole_Papillon_Damour.Domain.Common.Models;
using Vole_Papillon_Damour.Domain.RecommendationAggregate.ValueObjects;

namespace Vole_Papillon_Damour.Domain.RecommendationAggregate;

/// <summary>Persistent state and retry checkpoint for one book's recommendation work.</summary>
public sealed class BookRecommendationJob : Entity<string>
{
    public string Isbn13
    {
        get => Id;
        private set => Id = value;
    }

    public BookRecommendationJobStatus Status { get; private set; } = BookRecommendationJobStatus.Pending;
    public BookRecommendationJobStage Stage { get; private set; } = BookRecommendationJobStage.Notice;
    public int AttemptCount { get; private set; }
    public int RequestVersion { get; private set; }
    public int Revision { get; private set; }
    public DateTime RequestedAt { get; private set; }
    public DateTime? StartedAt { get; private set; }
    public DateTime? CompletedAt { get; private set; }
    public string? LastError { get; private set; }

    private BookRecommendationJob(string isbn13) : base(isbn13)
    {
    }

    private BookRecommendationJob()
    {
    }

    public static BookRecommendationJob Create(string isbn13)
    {
        if (!Isbn13Check.IsCanonical(isbn13))
        {
            throw new ArgumentException("A canonical ISBN-13 is required.", nameof(isbn13));
        }

        return new BookRecommendationJob(isbn13);
    }

    /// <summary>Moves an eligible job back to the queue and returns whether it should be published.</summary>
    public bool Request(DateTime requestedAt, bool force = false)
    {
        requestedAt = DomainTime.RequireUtc(requestedAt, nameof(requestedAt));
        if (!force && Status is (BookRecommendationJobStatus.InProgress or
            BookRecommendationJobStatus.Ready or
            BookRecommendationJobStatus.Failed or
            BookRecommendationJobStatus.NoNotice))
        {
            return false;
        }

        RequestVersion = checked(RequestVersion + 1);
        RequestedAt = requestedAt;
        CompletedAt = null;
        LastError = null;
        if (force)
        {
            Stage = BookRecommendationJobStage.Notice;
        }

        if (Status != BookRecommendationJobStatus.InProgress)
        {
            Status = BookRecommendationJobStatus.Pending;
        }

        Touch();
        return true;
    }

    public bool TryStart(int requestVersion, DateTime startedAt)
    {
        startedAt = DomainTime.RequireUtc(startedAt, nameof(startedAt));
        if (requestVersion != RequestVersion ||
            Status is not (BookRecommendationJobStatus.Pending or BookRecommendationJobStatus.InProgress))
        {
            return false;
        }

        Status = BookRecommendationJobStatus.InProgress;
        AttemptCount = checked(AttemptCount + 1);
        StartedAt = startedAt;
        CompletedAt = null;
        LastError = null;
        Touch();
        return true;
    }

    public void AdvanceTo(BookRecommendationJobStage stage)
    {
        if (Status != BookRecommendationJobStatus.InProgress)
        {
            throw new InvalidOperationException("Only an in-progress recommendation job can advance stages.");
        }

        if ((byte)stage < (byte)Stage)
        {
            throw new ArgumentOutOfRangeException(nameof(stage), "Recommendation stages cannot move backwards.");
        }

        if (Stage == stage)
        {
            return;
        }

        Stage = stage;
        Touch();
    }

    public void Complete(
        int requestVersion,
        BookRecommendationJobStatus status,
        DateTime completedAt)
    {
        completedAt = DomainTime.RequireUtc(completedAt, nameof(completedAt));
        if (status is not (BookRecommendationJobStatus.Ready or BookRecommendationJobStatus.NoNotice))
        {
            throw new ArgumentOutOfRangeException(nameof(status), "A recommendation job can complete only as ready or without a notice.");
        }

        if (Status != BookRecommendationJobStatus.InProgress)
        {
            throw new InvalidOperationException("Only an in-progress recommendation job can complete.");
        }

        if (requestVersion < RequestVersion)
        {
            Status = BookRecommendationJobStatus.Pending;
            Stage = BookRecommendationJobStage.Notice;
            LastError = null;
            Touch();
            return;
        }

        Status = status;
        CompletedAt = completedAt;
        LastError = null;
        Touch();
    }

    public void Fail(
        int requestVersion,
        BookRecommendationJobStage stage,
        string error,
        DateTime failedAt)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(error);
        failedAt = DomainTime.RequireUtc(failedAt, nameof(failedAt));
        if (Status != BookRecommendationJobStatus.InProgress)
        {
            throw new InvalidOperationException("Only an in-progress recommendation job can fail.");
        }

        if (requestVersion < RequestVersion)
        {
            Status = BookRecommendationJobStatus.Pending;
            Stage = BookRecommendationJobStage.Notice;
            LastError = null;
            Touch();
            return;
        }

        Stage = stage;
        Status = BookRecommendationJobStatus.Failed;
        CompletedAt = failedAt;
        LastError = error.Length <= 2000 ? error : error[..2000];
        Touch();
    }

    private void Touch() => Revision = checked(Revision + 1);
}
