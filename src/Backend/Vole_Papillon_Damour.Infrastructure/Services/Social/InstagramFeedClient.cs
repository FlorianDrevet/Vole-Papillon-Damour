using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using Microsoft.Extensions.Options;
using Vole_Papillon_Damour.Application.Common.Interfaces.Services;
using Vole_Papillon_Damour.Application.Common.Models;

namespace Vole_Papillon_Damour.Infrastructure.Services.Social;

public sealed class InstagramFeedClient(
    HttpClient httpClient,
    IOptions<InstagramOptions> options) : ISocialFeedClient
{
    private const string Fields =
        "id,caption,media_type,media_url,thumbnail_url,permalink,timestamp,children{id,media_type,media_url,thumbnail_url}";
    private readonly InstagramOptions _options = options.Value;

    public async Task<IReadOnlyList<SocialPost>> GetRecentPostsAsync(
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(_options.UserId) ||
            string.IsNullOrWhiteSpace(_options.AccessToken))
        {
            throw new SocialFeedAuthenticationException(
                "The Instagram account and access token must be configured.");
        }

        using var request = new HttpRequestMessage(
            HttpMethod.Get,
            BuildMediaEndpoint());
        request.Headers.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            _options.AccessToken);

        using var response = await httpClient.SendAsync(request, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            ThrowForFailure(response.StatusCode, body);
        }

        using var document = JsonDocument.Parse(body);
        ThrowForGraphError(document.RootElement);

        if (!document.RootElement.TryGetProperty("data", out var data) ||
            data.ValueKind != JsonValueKind.Array)
        {
            throw new HttpRequestException("The Instagram response did not contain a data array.");
        }

        var posts = new List<SocialPost>();
        foreach (var item in data.EnumerateArray())
        {
            if (!TryReadPost(item, out var post))
            {
                continue;
            }

            posts.Add(post);
        }

        return posts;
    }

    private Uri BuildMediaEndpoint()
    {
        var version = _options.GraphApiVersion.Trim('/');
        var userId = Uri.EscapeDataString(_options.UserId.Trim());
        var fields = Uri.EscapeDataString(Fields);
        return new Uri(
            $"{version}/{userId}/media?fields={fields}&limit=100",
            UriKind.Relative);
    }

    private static bool TryReadPost(JsonElement item, out SocialPost post)
    {
        post = null!;
        if (!TryGetString(item, "id", out var externalId) ||
            !TryGetString(item, "permalink", out var permalinkValue) ||
            !Uri.TryCreate(permalinkValue, UriKind.Absolute, out var permalink) ||
            !TryGetString(item, "timestamp", out var timestampValue) ||
            !DateTimeOffset.TryParse(
                timestampValue,
                CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal,
                out var publishedAt))
        {
            return false;
        }

        var medias = ReadMedias(item);
        post = new SocialPost(
            externalId,
            TryGetString(item, "caption", out var caption) ? caption : null,
            permalink,
            publishedAt.ToUniversalTime(),
            medias);
        return true;
    }

    private static IReadOnlyList<SocialPostMedia> ReadMedias(JsonElement item)
    {
        if (TryGetString(item, "media_type", out var mediaType) &&
            string.Equals(mediaType, "CAROUSEL_ALBUM", StringComparison.OrdinalIgnoreCase) &&
            item.TryGetProperty("children", out var children) &&
            children.TryGetProperty("data", out var childData) &&
            childData.ValueKind == JsonValueKind.Array)
        {
            return childData
                .EnumerateArray()
                .Select(ReadMedia)
                .Where(media => media is not null)
                .Select(media => media!)
                .ToList();
        }

        var singleMedia = ReadMedia(item);
        return singleMedia is null ? [] : [singleMedia];
    }

    private static SocialPostMedia? ReadMedia(JsonElement item)
    {
        if (!TryGetString(item, "media_type", out var mediaType))
        {
            return null;
        }

        Uri? contentUrl = TryGetUri(item, "media_url");
        Uri? thumbnailUrl = TryGetUri(item, "thumbnail_url");
        if (string.Equals(mediaType, "VIDEO", StringComparison.OrdinalIgnoreCase))
        {
            return new SocialPostMedia(SocialMediaKind.Video, contentUrl, thumbnailUrl);
        }

        if (string.Equals(mediaType, "IMAGE", StringComparison.OrdinalIgnoreCase))
        {
            return new SocialPostMedia(SocialMediaKind.Image, contentUrl, thumbnailUrl);
        }

        return null;
    }

    private static Uri? TryGetUri(JsonElement item, string propertyName)
    {
        return TryGetString(item, propertyName, out var value) &&
               Uri.TryCreate(value, UriKind.Absolute, out var uri)
            ? uri
            : null;
    }

    private static bool TryGetString(
        JsonElement item,
        string propertyName,
        out string value)
    {
        if (item.TryGetProperty(propertyName, out var property) &&
            property.ValueKind == JsonValueKind.String)
        {
            value = property.GetString() ?? string.Empty;
            return !string.IsNullOrWhiteSpace(value);
        }

        value = string.Empty;
        return false;
    }

    private static void ThrowForGraphError(JsonElement root)
    {
        if (!root.TryGetProperty("error", out var error))
        {
            return;
        }

        var message = TryGetString(error, "message", out var errorMessage)
            ? errorMessage
            : "The Instagram API returned an error.";
        var code = error.TryGetProperty("code", out var codeProperty) &&
                   codeProperty.TryGetInt32(out var parsedCode)
            ? parsedCode
            : 0;
        if (code == 190)
        {
            throw new SocialFeedAuthenticationException(message);
        }

        if (code is 4 or 17 or 613)
        {
            throw new SocialFeedQuotaException(message);
        }

        throw new HttpRequestException(message);
    }

    private static void ThrowForFailure(HttpStatusCode statusCode, string body)
    {
        if (statusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
        {
            throw new SocialFeedAuthenticationException(
                "The Instagram access token was refused.");
        }

        if ((int)statusCode == 429)
        {
            throw new SocialFeedQuotaException(
                "The Instagram API rate limit was reached.");
        }

        throw new HttpRequestException(
            $"The Instagram API returned HTTP {(int)statusCode}.");
    }
}
