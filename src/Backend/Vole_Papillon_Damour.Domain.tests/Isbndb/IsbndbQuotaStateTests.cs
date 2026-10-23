using FluentAssertions;
using Vole_Papillon_Damour.Domain.IsbndbAggregate;

namespace Vole_Papillon_Damour.Domain.tests.Isbndb;

public sealed class IsbndbQuotaStateTests
{
    private static readonly DateTime Now = new(2026, 10, 23, 10, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Observe_WhenQuotaIsZero_MarksQuotaExhaustedUntilReset()
    {
        var state = new IsbndbQuotaState();
        var resetAt = Now.AddHours(14);

        state.Observe(5_000, 0, resetAt, Now);

        state.Id.Should().Be(IsbndbQuotaState.SingletonId);
        state.DailyLimit.Should().Be(5_000);
        state.DailyRemaining.Should().Be(0);
        state.ResetsAt.Should().Be(resetAt);
        state.ObservedAt.Should().Be(Now);
        state.ExhaustedUntil.Should().Be(resetAt);
        state.IsExhausted(resetAt.AddMinutes(-1)).Should().BeTrue();
        state.Status(Now).Should().Be(IsbndbQuotaStatus.Exhausted);
    }

    [Fact]
    public void Observe_AfterQuotaReset_ClearsExhaustion()
    {
        var state = new IsbndbQuotaState();
        var previousReset = Now.AddHours(1);
        state.Observe(5_000, 0, previousReset, Now);

        state.Observe(5_000, 4_998, previousReset.AddDays(1), previousReset.AddSeconds(1));

        state.DailyRemaining.Should().Be(4_998);
        state.ExhaustedUntil.Should().BeNull();
        state.IsExhausted(previousReset.AddSeconds(1)).Should().BeFalse();
        state.Status(previousReset.AddSeconds(1)).Should().Be(IsbndbQuotaStatus.Operational);
    }

    [Fact]
    public void MarkExhausted_SetsTheZeroRemainingQuotaAndResetTime()
    {
        var state = new IsbndbQuotaState();
        var resetAt = Now.AddHours(1);

        state.MarkExhausted(resetAt, Now);

        state.DailyRemaining.Should().Be(0);
        state.ResetsAt.Should().Be(resetAt);
        state.ExhaustedUntil.Should().Be(resetAt);
        state.ObservedAt.Should().Be(Now);
    }

    [Fact]
    public void Status_RecentFailureIsUnavailableUntilAValidationSucceedsOrFifteenMinutesPass()
    {
        var state = new IsbndbQuotaState();
        state.RecordFailure("API indisponible", Now);

        state.Status(Now.AddMinutes(14)).Should().Be(IsbndbQuotaStatus.Unavailable);
        state.Status(Now.AddMinutes(15)).Should().Be(IsbndbQuotaStatus.Operational);

        state.RecordFailure("Nouvelle erreur", Now.AddMinutes(16));
        state.Observe(5_000, 4_999, Now.AddDays(1), Now.AddMinutes(17));

        state.Status(Now.AddMinutes(17)).Should().Be(IsbndbQuotaStatus.Operational);
        state.LastFailureReason.Should().Be("Nouvelle erreur");
    }

    [Fact]
    public void RecordFailure_TrimsAndLimitsTheStoredReason()
    {
        var state = new IsbndbQuotaState();

        state.RecordFailure($"  {new string('x', 510)}  ", Now);

        state.LastFailureAt.Should().Be(Now);
        state.LastFailureReason.Should().HaveLength(500);
    }

    [Fact]
    public void DailyUsage_IncrementTracksCreditsAndRejectsNonPositiveAmounts()
    {
        var usage = IsbndbDailyUsage.Create(new DateOnly(2026, 10, 23), IsbndbUsage.CatchUp);
        usage.Increment();
        usage.Increment(4);

        usage.Day.Should().Be(new DateOnly(2026, 10, 23));
        usage.Usage.Should().Be(IsbndbUsage.CatchUp);
        usage.Credits.Should().Be(5);
        var invalidIncrement = () => usage.Increment(0);
        invalidIncrement.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void CatchUpRun_FinishStoresCountersAndStopReason()
    {
        var run = IsbndbCatchUpRun.Create(Now);
        var endedAt = Now.AddMinutes(4);

        run.Finish(endedAt, filled: 12, absent: 5, rechecked: 3, remaining: 24, IsbndbCatchUpStopReason.QuotaExhausted);

        run.StartedAt.Should().Be(Now);
        run.EndedAt.Should().Be(endedAt);
        run.Filled.Should().Be(12);
        run.Absent.Should().Be(5);
        run.Rechecked.Should().Be(3);
        run.Remaining.Should().Be(24);
        run.StopReason.Should().Be(IsbndbCatchUpStopReason.QuotaExhausted);
    }
}
