using Vole_Papillon_Damour.Domain.AssoEventsAggregate;
using Vole_Papillon_Damour.Domain.EventsAggregate.ValueObjects;

namespace Vole_Papillon_Damour.Application.Books.Common;

internal sealed record BookFairMatch(
    AssoEventsId? AssoEventsId,
    SaleFairMatchStatus Status,
    string? Note);

internal static class BookFairResolver
{
    internal const string NoOpenFairNote = "Sale.NoOpenFair";
    internal const string OverlappingOpenFairsNote = "Sale.OverlappingOpenFairs";

    public static BookFairMatch Resolve(
        IEnumerable<AssoEvents> events,
        DateTime instantUtc)
    {
        var openBookFairs = events
            .Where(assoEvent =>
                !assoEvent.IsCancelled &&
                assoEvent.EventsType?.Value == EventsType.EventsTypeEnum.Books &&
                IsOpen(assoEvent, instantUtc))
            .ToList();

        return openBookFairs.Count switch
        {
            0 => new BookFairMatch(null, SaleFairMatchStatus.NoOpenFair, NoOpenFairNote),
            1 => new BookFairMatch(
                openBookFairs[0].Id,
                SaleFairMatchStatus.Attached,
                null),
            _ => new BookFairMatch(null, SaleFairMatchStatus.OverlappingOpenFairs, OverlappingOpenFairsNote)
        };
    }

    public static SaleFairMatchStatus FromNote(string? note)
    {
        return note switch
        {
            OverlappingOpenFairsNote => SaleFairMatchStatus.OverlappingOpenFairs,
            NoOpenFairNote => SaleFairMatchStatus.NoOpenFair,
            _ => SaleFairMatchStatus.Attached
        };
    }

    public static bool IsOpen(AssoEvents assoEvent, DateTime instantUtc)
    {
        var openAtUtc = (assoEvent.HourOpenDoors ?? assoEvent.DateStart).UtcDateTime;
        var closeAtUtc = assoEvent.HourCloseDoors?.UtcDateTime
            ?? assoEvent.DateEnd?.UtcDateTime
            ?? NextParisMidnightUtc(assoEvent.DateStart);

        return openAtUtc < closeAtUtc &&
               openAtUtc <= instantUtc &&
               instantUtc < closeAtUtc;
    }

    private static DateTime NextParisMidnightUtc(DateTimeOffset dateStart)
    {
        var parisDate = TimeZoneInfo.ConvertTime(dateStart, ParisTimeZone).Date;
        var nextMidnight = parisDate.AddDays(1);
        return TimeZoneInfo.ConvertTimeToUtc(nextMidnight, ParisTimeZone);
    }

    private static TimeZoneInfo ParisTimeZone =>
        TimeZoneInfo.FindSystemTimeZoneById(
            OperatingSystem.IsWindows() ? "Romance Standard Time" : "Europe/Paris");
}
