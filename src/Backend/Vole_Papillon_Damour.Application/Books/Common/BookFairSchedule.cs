using Vole_Papillon_Damour.Domain.AssoEventsAggregate;

namespace Vole_Papillon_Damour.Application.Books.Common;

/// <summary>
/// Resolves the instants of a book fair from the legacy event date contract.
/// </summary>
/// <remarks>
/// The administration UI stores a book fair date and its opening/closing wall-clock
/// values as separate UTC components. The date on the hour value is therefore not
/// authoritative and can be left over from the day on which the event was edited.
/// </remarks>
public static class BookFairSchedule
{
    public static DateTimeOffset GetOpeningInstant(AssoEvents assoEvent)
    {
        return assoEvent.HourOpenDoors is { } opening
            ? CombineUtcDateAndWallClock(assoEvent.DateStart, opening)
            : assoEvent.DateStart;
    }

    public static DateTimeOffset? GetClosingInstant(AssoEvents assoEvent)
    {
        return assoEvent.HourCloseDoors is { } closing
            ? CombineUtcDateAndWallClock(assoEvent.DateEnd ?? assoEvent.DateStart, closing)
            : assoEvent.DateEnd;
    }

    private static DateTimeOffset CombineUtcDateAndWallClock(
        DateTimeOffset date,
        DateTimeOffset wallClock)
    {
        var utcDate = date.UtcDateTime.Date;
        var utcTime = wallClock.UtcDateTime.TimeOfDay;
        return new DateTimeOffset(utcDate.Add(utcTime), TimeSpan.Zero);
    }
}
