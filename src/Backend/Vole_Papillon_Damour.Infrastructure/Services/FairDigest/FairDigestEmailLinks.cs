using Vole_Papillon_Damour.Application.Books.Common;
using Vole_Papillon_Damour.Domain.BookAggregate.ValueObjects;
using Vole_Papillon_Damour.Infrastructure.Services.BookAlerts;

namespace Vole_Papillon_Damour.Infrastructure.Services.FairDigest;

public sealed record FairDigestEmailLinks(
    string AssociationName,
    string? LogoUrl,
    string CatalogBaseUrl,
    string WebsiteUrl,
    string? UnsubscribeUrl)
{
    public string SuggestionsUrl => $"{CatalogBaseUrl.TrimEnd('/')}/trouver-mon-prochain-livre";

    public string WebsiteLabel =>
        Uri.TryCreate(WebsiteUrl, UriKind.Absolute, out var uri) ? uri.Host : WebsiteUrl;

    public string? HttpsLogoUrl => BookAlertEmailLinks.HttpsOrNull(LogoUrl);

    public string? BookUrl(PublicCatalogBookResult book)
    {
        ArgumentNullException.ThrowIfNull(book);
        return Isbn13.TryCreate(book.Isbn13, out var isbn13)
            ? $"{CatalogBaseUrl.TrimEnd('/')}{PublicBookPath.For(isbn13, book.Title, book.Authors)}"
            : null;
    }
}
