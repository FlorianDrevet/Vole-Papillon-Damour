using FluentAssertions;
using Vole_Papillon_Damour.Application.FairDigest.Common;
using Vole_Papillon_Damour.Domain.EventsAggregate.ValueObjects;

namespace Vole_Papillon_Damour.Application.tests.FairDigest;

public sealed class FairDigestScheduleTests
{
    [Theory]
    [InlineData("2027-03-13T09:00:00+01:00", "2027-03-10T09:00:00+00:00")]
    [InlineData("2027-04-10T09:00:00+02:00", "2027-04-07T08:00:00+00:00")]
    public void Calculate_UsesParisSendHourAcrossDaylightSavingChanges(string start, string expectedSend)
    {
        var fair = Fair(DateTimeOffset.Parse(start));

        var schedule = FairDigestSchedule.Calculate(
            [fair],
            DateTimeOffset.Parse("2027-01-01T00:00:00+00:00"),
            daysBefore: 3,
            sendHourParis: 10);

        schedule.NextFair.Should().Be(fair);
        schedule.PlannedAtUtc.Should().Be(DateTimeOffset.Parse(expectedSend));
    }

    [Fact]
    public void Calculate_SchedulesImmediatelyWhenFairIsTwoDaysAway()
    {
        var now = DateTimeOffset.Parse("2027-02-08T09:00:00+00:00");
        var fair = Fair(DateTimeOffset.Parse("2027-02-10T10:00:00+01:00"));

        var schedule = FairDigestSchedule.Calculate([fair], now, daysBefore: 3, sendHourParis: 10);

        schedule.PlannedAtUtc.Should().Be(now);
    }

    [Fact]
    public void Calculate_SkipsFairWhenItIsLessThanTwentyFourHoursAway()
    {
        var fair = Fair(DateTimeOffset.Parse("2027-02-10T09:00:00+01:00"));

        var schedule = FairDigestSchedule.Calculate(
            [fair],
            DateTimeOffset.Parse("2027-02-09T12:00:00+00:00"),
            daysBefore: 3,
            sendHourParis: 10);

        schedule.NextFair.Should().Be(fair);
        schedule.PlannedAtUtc.Should().BeNull();
    }

    [Fact]
    public void Calculate_SelectsNearestActiveBookFairAndItsPreviousFair()
    {
        var previous = Fair(
            DateTimeOffset.Parse("2027-01-10T10:00:00+01:00"),
            DateTimeOffset.Parse("2027-01-10T18:00:00+01:00"));
        var cancelled = Fair(
            DateTimeOffset.Parse("2027-02-10T10:00:00+01:00"),
            cancelled: true);
        var otherEvent = Fair(
            DateTimeOffset.Parse("2027-02-15T10:00:00+01:00"),
            type: EventsType.EventsTypeEnum.Other);
        var next = Fair(DateTimeOffset.Parse("2027-03-13T09:00:00+01:00"));

        var schedule = FairDigestSchedule.Calculate(
            [next, previous, cancelled, otherEvent],
            DateTimeOffset.Parse("2027-02-01T00:00:00+00:00"),
            daysBefore: 3,
            sendHourParis: 10);

        schedule.NextFair.Should().Be(next);
        schedule.PreviousFair.Should().Be(previous);
        schedule.PreviousFairEndsAtUtc.Should().Be(DateTimeOffset.Parse("2027-01-10T23:00:00+00:00"));
    }

    private static FairDigestEvent Fair(
        DateTimeOffset start,
        DateTimeOffset? end = null,
        bool cancelled = false,
        EventsType.EventsTypeEnum type = EventsType.EventsTypeEnum.Books) =>
        new(Guid.NewGuid(), type, start, end, cancelled);
}
