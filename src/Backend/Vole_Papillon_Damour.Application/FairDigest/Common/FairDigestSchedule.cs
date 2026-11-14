using Vole_Papillon_Damour.Application.Common;
using Vole_Papillon_Damour.Domain.AssoEventsAggregate.ValueObjects;
using Vole_Papillon_Damour.Domain.EventsAggregate.ValueObjects;

namespace Vole_Papillon_Damour.Application.FairDigest.Common;

public sealed record FairDigestEvent(
    Guid Id,
    EventsType.EventsTypeEnum Type,
    DateTimeOffset DateStart,
    DateTimeOffset? DateEnd,
    bool IsCancelled,
    string Name = "Bourse aux livres",
    string? Address = null)
{
    public static string? FormatAddress(Adresse? address)
    {
        if (address is null)
        {
            return null;
        }

        var street = string.Join(" ", new[]
        {
            address.RoadNumber?.ToString(System.Globalization.CultureInfo.InvariantCulture),
            address.Road
        }.Where(part => !string.IsNullOrWhiteSpace(part)));
        var city = string.Join(" ", new[]
        {
            address.CityCode.ToString(System.Globalization.CultureInfo.InvariantCulture),
            address.City
        }.Where(part => !string.IsNullOrWhiteSpace(part)));
        return string.Join(", ", new[] { street, city }.Where(part => !string.IsNullOrWhiteSpace(part)));
    }
}

public sealed record FairDigestScheduleResult(
    FairDigestEvent? NextFair,
    FairDigestEvent? PreviousFair,
    DateTimeOffset? PlannedAtUtc,
    DateTimeOffset? PreviousFairEndsAtUtc);

public static class FairDigestSchedule
{
    private static readonly TimeSpan MinimumNotice = TimeSpan.FromHours(24);

    public static FairDigestScheduleResult Calculate(
        IEnumerable<FairDigestEvent> events,
        DateTimeOffset nowUtc,
        int daysBefore,
        int sendHourParis)
    {
        ArgumentNullException.ThrowIfNull(events);
        if (daysBefore < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(daysBefore));
        }

        if (sendHourParis is < 0 or > 23)
        {
            throw new ArgumentOutOfRangeException(nameof(sendHourParis));
        }

        var bookFairs = events
            .Where(fair => !fair.IsCancelled && fair.Type == EventsType.EventsTypeEnum.Books)
            .OrderBy(fair => fair.DateStart)
            .ThenBy(fair => fair.Id)
            .ToArray();
        var nextFair = bookFairs.FirstOrDefault(fair => fair.DateStart > nowUtc);
        if (nextFair is null)
        {
            return new FairDigestScheduleResult(null, null, null, null);
        }

        var previousFair = bookFairs
            .Where(fair => fair.DateStart < nextFair.DateStart)
            .LastOrDefault();
        DateTimeOffset? previousFairEndsAtUtc = previousFair is null
            ? null
            : EndOfFairDay(previousFair.DateEnd ?? previousFair.DateStart);
        DateTimeOffset? plannedAtUtc = GetPlannedAtUtc(nextFair.DateStart, daysBefore, sendHourParis);
        var timeUntilOpening = nextFair.DateStart - nowUtc;
        if (plannedAtUtc.Value <= nowUtc)
        {
            plannedAtUtc = timeUntilOpening >= MinimumNotice ? nowUtc : null;
        }

        return new FairDigestScheduleResult(
            nextFair,
            previousFair,
            plannedAtUtc,
            previousFairEndsAtUtc);
    }

    private static DateTimeOffset GetPlannedAtUtc(
        DateTimeOffset fairStart,
        int daysBefore,
        int sendHourParis)
    {
        var fairStartInParis = TimeZoneInfo.ConvertTime(fairStart, ParisCalendar.Zone);
        var plannedParis = DateTime.SpecifyKind(
            fairStartInParis.DateTime.Date.AddDays(-daysBefore).AddHours(sendHourParis),
            DateTimeKind.Unspecified);
        return new DateTimeOffset(
            TimeZoneInfo.ConvertTimeToUtc(plannedParis, ParisCalendar.Zone),
            TimeSpan.Zero);
    }

    public static DateTimeOffset EndOfFairDay(DateTimeOffset instant)
    {
        var parisDate = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(instant, ParisCalendar.Zone).DateTime);
        var nextMidnightParis = parisDate.AddDays(1).ToDateTime(TimeOnly.MinValue, DateTimeKind.Unspecified);
        return new DateTimeOffset(
            TimeZoneInfo.ConvertTimeToUtc(nextMidnightParis, ParisCalendar.Zone),
            TimeSpan.Zero);
    }
}
