namespace Vole_Papillon_Damour.Infrastructure.Services.BookAlerts;

public sealed class UnsubscribeTokenOptions
{
    public const string SectionName = "BookAlerts:Unsubscribe";

    /// <summary>Base64 or raw shared secret signing the tokens.</summary>
    public string SigningKey { get; set; } = string.Empty;

    /// <summary>
    /// Absolute URL of the one-click endpoint, without query string.
    /// </summary>
    public string Endpoint { get; set; } = string.Empty;

    /// <summary>
    /// How long a minted token stays valid. Alert emails are read late, so this
    /// is generous by design; the token only ever suspends alerts.
    /// </summary>
    public int LifetimeDays { get; set; } = 180;

    /// <summary>
    /// Where a human landing on the unsubscribe URL with a GET is sent.
    /// </summary>
    public string AccountUrl { get; set; } = string.Empty;
}
