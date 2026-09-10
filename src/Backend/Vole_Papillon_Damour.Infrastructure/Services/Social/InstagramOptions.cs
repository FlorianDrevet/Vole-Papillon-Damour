namespace Vole_Papillon_Damour.Infrastructure.Services.Social;

public sealed class InstagramOptions
{
    private const int AccessTokenLifetimeDays = 60;
    private const int AccessTokenWarningDays = 15;

    public const string SectionName = "SocialImport";

    public string GraphApiVersion { get; set; } = "v22.0";
    public string ApiBaseUrl { get; set; } = "https://graph.instagram.com/";
    public string UserId { get; set; } = string.Empty;
    public string AccessToken { get; set; } = string.Empty;
    public DateTimeOffset? AccessTokenIssuedAt { get; set; }
    public int TimeoutMilliseconds { get; set; } = 10_000;
    public string UserAgent { get; set; } = "VolePapillonDamour/1.0";

    public bool IsAccessTokenNearExpiry(DateTimeOffset now)
    {
        return AccessTokenIssuedAt is { } issuedAt &&
               now >= issuedAt.AddDays(AccessTokenLifetimeDays - AccessTokenWarningDays);
    }
}
