namespace Vole_Papillon_Damour.Application.Common;

/// <summary>
/// Calendar days as the association lives them: an event is "today" according to
/// the clock in Paris, whatever the server's time zone.
/// </summary>
public static class ParisCalendar
{
    public static TimeZoneInfo Zone { get; } = TimeZoneInfo.FindSystemTimeZoneById(
        OperatingSystem.IsWindows() ? "Romance Standard Time" : "Europe/Paris");

    public static DateOnly Today(DateTime utcNow)
    {
        var utc = DateTime.SpecifyKind(utcNow, DateTimeKind.Utc);
        return DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(utc, Zone));
    }

    public static DateTimeOffset StartOfDayUtc(DateOnly day)
    {
        var parisMidnight = day.ToDateTime(TimeOnly.MinValue, DateTimeKind.Unspecified);
        return new DateTimeOffset(TimeZoneInfo.ConvertTimeToUtc(parisMidnight, Zone), TimeSpan.Zero);
    }
}
