namespace Vole_Papillon_Damour.Application.Books.Queries.Admin;

/// <summary>
/// Named thresholds used by responsible-facing scan-session views.
/// </summary>
public static class AdminScanSessionPolicy
{
    /// <summary>Age after which an open session is considered stale for administration.</summary>
    public const int StaleSessionAgeHours = 24;

    public static TimeSpan StaleSessionAge => TimeSpan.FromHours(StaleSessionAgeHours);
}
