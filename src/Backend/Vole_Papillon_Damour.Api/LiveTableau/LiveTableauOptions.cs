namespace Vole_Papillon_Damour.Api.LiveTableau;

public sealed class LiveTableauOptions
{
    public const string SectionName = "LiveTableau";

    /// <summary>How often a replica checks the database for changes made on another replica.</summary>
    public TimeSpan SyncInterval { get; set; } = TimeSpan.FromSeconds(1);

    /// <summary>Maximum silence on a stream; proxies close idle connections.</summary>
    public TimeSpan HeartbeatInterval { get; set; } = TimeSpan.FromSeconds(15);

    /// <summary>Reconnection delay suggested to the browser EventSource.</summary>
    public TimeSpan ReconnectionInterval { get; set; } = TimeSpan.FromSeconds(3);
}
