namespace Vole_Papillon_Damour.Contracts.Events.Responses;

/// <summary>The event announced in the site's banner, up to seven days ahead.</summary>
public class TodayEventResponse
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string EventType { get; set; } = string.Empty;
    public DateTimeOffset DateStart { get; set; }
    public DateTimeOffset? DateEnd { get; set; }
    public DateTimeOffset? HourOpenDoors { get; set; }
    public DateTimeOffset? HourCloseDoors { get; set; }
    public string City { get; set; } = string.Empty;

    /// <summary><c>NotStarted</c>, <c>Live</c> or <c>Finished</c> for a loto; <c>null</c> otherwise.</summary>
    public string? LiveStatus { get; set; }

    public int? LastNumero { get; set; }

    /// <summary><c>Classic</c> or <c>Halloween</c> for a loto; <c>null</c> otherwise.</summary>
    public string? LiveTheme { get; set; }
}
