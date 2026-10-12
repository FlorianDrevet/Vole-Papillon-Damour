namespace Vole_Papillon_Damour.Infrastructure.Services.BookAlerts;

/// <summary>Identité de l'association et adresses citées par une alerte.</summary>
public sealed record BookAlertEmailLinks(
    string AssociationName,
    string? LogoUrl,
    string CatalogBaseUrl,
    string WebsiteUrl,
    string? UnsubscribeUrl)
{
    public string SearchListUrl => $"{CatalogBaseUrl.TrimEnd('/')}/compte";

    public string WebsiteLabel =>
        Uri.TryCreate(WebsiteUrl, UriKind.Absolute, out var uri) ? uri.Host : WebsiteUrl;

    public string? HttpsLogoUrl => HttpsOrNull(LogoUrl);

    public string? Unsubscribe => string.IsNullOrWhiteSpace(UnsubscribeUrl) ? null : UnsubscribeUrl;

    public string? BookUrl(string? publicPath)
    {
        return string.IsNullOrWhiteSpace(publicPath)
            ? null
            : $"{CatalogBaseUrl.TrimEnd('/')}{publicPath}";
    }

    /// <summary>L'URL absolue si elle est en HTTPS, sinon null (images d'un e-mail).</summary>
    public static string? HttpsOrNull(string? url)
    {
        return Uri.TryCreate(url, UriKind.Absolute, out var uri) && uri.Scheme == Uri.UriSchemeHttps
            ? uri.AbsoluteUri
            : null;
    }
}
