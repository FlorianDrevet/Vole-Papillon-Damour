using System.Net;
using Microsoft.Extensions.Options;
using Vole_Papillon_Damour.Application.Common.Interfaces.Services;
using Vole_Papillon_Damour.Application.Common.Models;
using Vole_Papillon_Damour.Infrastructure.Services.BookAlerts;

namespace Vole_Papillon_Damour.Infrastructure.Services.FairDigest;

public sealed class FairDigestEmailRenderer(
    IOptions<BookAlertEmailOptions> emailOptions,
    IOptions<UnsubscribeTokenOptions> unsubscribeOptions) : IFairDigestEmailRenderer
{
    private readonly BookAlertEmailOptions _emailOptions = emailOptions.Value;
    private readonly UnsubscribeTokenOptions _unsubscribeOptions = unsubscribeOptions.Value;

    public FairDigestEmailContent Render(FairDigestEmailMessage message, string unsubscribeToken)
    {
        ArgumentNullException.ThrowIfNull(message);
        ArgumentException.ThrowIfNullOrWhiteSpace(unsubscribeToken);
        var endpoint = _unsubscribeOptions.Endpoint;
        var unsubscribeUrl = string.IsNullOrWhiteSpace(endpoint)
            ? null
            : $"{endpoint}{(endpoint.Contains('?', StringComparison.Ordinal) ? "&" : "?")}token={WebUtility.UrlEncode(unsubscribeToken)}";
        var links = new FairDigestEmailLinks(
            _emailOptions.AssociationName,
            _emailOptions.LogoUrl,
            _emailOptions.CatalogBaseUrl,
            _emailOptions.WebsiteUrl,
            unsubscribeUrl);
        return FairDigestEmailContentBuilder.Build(message, links);
    }
}
