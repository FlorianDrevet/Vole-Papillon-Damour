using System.Globalization;
using System.Text.Json;
using Microsoft.Extensions.Options;
using Vole_Papillon_Damour.Application.Books.Common;
using Vole_Papillon_Damour.Application.Common.Interfaces.Services;
using Vole_Papillon_Damour.Domain.BookAggregate.ValueObjects;

namespace Vole_Papillon_Damour.Infrastructure.Services.Bibliographic;

public sealed class GoogleBooksClient(
    HttpClient httpClient,
    IOptions<BibliographicOptions> options) : IGoogleBooksClient
{
    private static readonly string[] ImageLinkNames =
    [
        "extraLarge",
        "large",
        "medium",
        "small",
        "thumbnail",
        "smallThumbnail",
    ];

    private readonly BibliographicOptions _options = options.Value;

    public async Task<BookMetadataResult?> FindAsync(
        Isbn13 isbn13,
        CancellationToken cancellationToken)
    {
        using var response = await httpClient.GetAsync(
            BuildRequestUri(isbn13),
            HttpCompletionOption.ResponseHeadersRead,
            cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            return null;
        }

        await using var responseStream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var document = await JsonDocument.ParseAsync(
            responseStream,
            cancellationToken: cancellationToken);
        if (!document.RootElement.TryGetProperty("items", out var items) ||
            items.ValueKind != JsonValueKind.Array)
        {
            return null;
        }

        JsonElement? selectedVolumeInfo = null;
        Uri? coverUri = null;
        foreach (var item in items.EnumerateArray())
        {
            if (!item.TryGetProperty("volumeInfo", out var volumeInfo) ||
                volumeInfo.ValueKind != JsonValueKind.Object ||
                !HasExactIsbn13(volumeInfo, isbn13.Value))
            {
                continue;
            }

            selectedVolumeInfo ??= volumeInfo;
            coverUri ??= await FindValidatedCoverUriAsync(volumeInfo, cancellationToken);
            if (coverUri is not null)
            {
                break;
            }
        }

        if (selectedVolumeInfo is not { } selected)
        {
            return null;
        }

        return new BookMetadataResult(
            isbn13.Value,
            ReadString(selected, "title"),
            ReadStrings(selected, "authors"),
            ReadString(selected, "publisher"),
            ParseYear(ReadString(selected, "publishedDate")),
            coverUri,
            "GoogleBooks",
            null,
            DateTimeOffset.UtcNow,
            coverUri is null ? null : "GoogleBooks");
    }

    private Uri BuildRequestUri(Isbn13 isbn13)
    {
        var query = new List<string>
        {
            $"q={Uri.EscapeDataString($"isbn:{isbn13.Value}")}",
            "maxResults=10",
        };

        if (!string.IsNullOrWhiteSpace(_options.GoogleBooksApiKey))
        {
            query.Add($"key={Uri.EscapeDataString(_options.GoogleBooksApiKey)}");
        }

        return new Uri(
            $"{_options.GoogleBooksEndpoint}?{string.Join('&', query)}",
            UriKind.Absolute);
    }

    private async Task<Uri?> FindValidatedCoverUriAsync(
        JsonElement volumeInfo,
        CancellationToken cancellationToken)
    {
        if (!volumeInfo.TryGetProperty("imageLinks", out var imageLinks) ||
            imageLinks.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        foreach (var imageLinkName in ImageLinkNames)
        {
            var imageUrl = ReadString(imageLinks, imageLinkName);
            if (!TryCreateHttpsUri(imageUrl, out var imageUri) ||
                !await CoverImageValidator.IsValidAsync(httpClient, imageUri, cancellationToken))
            {
                continue;
            }

            return imageUri;
        }

        return null;
    }

    private static bool HasExactIsbn13(JsonElement volumeInfo, string isbn13)
    {
        if (!volumeInfo.TryGetProperty("industryIdentifiers", out var identifiers) ||
            identifiers.ValueKind != JsonValueKind.Array)
        {
            return false;
        }

        return identifiers.EnumerateArray().Any(identifier =>
            string.Equals(
                ReadString(identifier, "type"),
                "ISBN_13",
                StringComparison.OrdinalIgnoreCase) &&
            string.Equals(
                NormalizeIsbn(ReadString(identifier, "identifier")),
                isbn13,
                StringComparison.Ordinal));
    }

    private static bool TryCreateHttpsUri(string? value, out Uri uri)
    {
        uri = null!;
        if (!Uri.TryCreate(value, UriKind.Absolute, out var candidate))
        {
            return false;
        }

        if (string.Equals(candidate.Scheme, Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase))
        {
            var builder = new UriBuilder(candidate)
            {
                Scheme = Uri.UriSchemeHttps,
                Port = -1,
            };
            candidate = builder.Uri;
        }

        if (!string.Equals(candidate.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        uri = candidate;
        return true;
    }

    private static string? NormalizeIsbn(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return string.Concat(value.Where(char.IsDigit));
    }

    private static string? ReadString(JsonElement element, string propertyName)
    {
        return element.TryGetProperty(propertyName, out var property) &&
               property.ValueKind == JsonValueKind.String
            ? property.GetString()
            : null;
    }

    private static string? ReadStrings(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var property) ||
            property.ValueKind != JsonValueKind.Array)
        {
            return null;
        }

        var values = property
            .EnumerateArray()
            .Where(item => item.ValueKind == JsonValueKind.String)
            .Select(item => item.GetString())
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value!.Trim())
            .ToArray();

        return values.Length == 0 ? null : string.Join(", ", values);
    }

    private static int? ParseYear(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        for (var index = 0; index <= value.Length - 4; index++)
        {
            var candidate = value.Substring(index, 4);
            if (candidate[0] is not ('1' or '2') ||
                !int.TryParse(candidate, NumberStyles.None, CultureInfo.InvariantCulture, out var year))
            {
                continue;
            }

            return year;
        }

        return null;
    }
}
