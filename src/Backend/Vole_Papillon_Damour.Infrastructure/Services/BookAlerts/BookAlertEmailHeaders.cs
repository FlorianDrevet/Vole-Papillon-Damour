using System.Net;

namespace Vole_Papillon_Damour.Infrastructure.Services.BookAlerts;

/// <summary>
/// Builds the RFC 8058 one-click unsubscribe headers. Mailbox providers read
/// <c>List-Unsubscribe-Post</c> as a promise that an unauthenticated POST to the
/// advertised URL unsubscribes the recipient, so the pair is emitted only when
/// a usable signed URL exists.
/// </summary>
public static class BookAlertEmailHeaders
{
    public const string ListUnsubscribe = "List-Unsubscribe";
    public const string ListUnsubscribePost = "List-Unsubscribe-Post";
    public const string OneClickDirective = "List-Unsubscribe=One-Click";

    public static IReadOnlyDictionary<string, string> BuildOneClickUnsubscribe(
        string? endpoint,
        string? token)
    {
        if (string.IsNullOrWhiteSpace(endpoint) || string.IsNullOrWhiteSpace(token))
        {
            return new Dictionary<string, string>();
        }

        var url = $"{endpoint}?token={WebUtility.UrlEncode(token)}";

        return new Dictionary<string, string>
        {
            [ListUnsubscribe] = $"<{url}>",
            [ListUnsubscribePost] = OneClickDirective
        };
    }
}
