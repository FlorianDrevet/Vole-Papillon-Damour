using System.Globalization;
using System.Text.Json;
using Microsoft.Extensions.Options;
using Vole_Papillon_Damour.Application.Books.Common;
using Vole_Papillon_Damour.Application.Common.Interfaces.Services;
using Vole_Papillon_Damour.Domain.BookAggregate.ValueObjects;

namespace Vole_Papillon_Damour.Infrastructure.Services.Bibliographic;

public sealed class OpenLibraryClient(
    HttpClient httpClient,
    IOptions<BibliographicOptions> options) : IOpenLibraryClient
{
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
        using var document = await JsonDocument.ParseAsync(responseStream, cancellationToken: cancellationToken);
        if (!document.RootElement.TryGetProperty("docs", out var documents) ||
            documents.ValueKind != JsonValueKind.Array ||
            documents.GetArrayLength() == 0)
        {
            return null;
        }

        var documentElement = documents[0];
        var coverId = ReadInt(documentElement, "cover_i");
        var workKey = ReadString(documentElement, "key");
        var workId = workKey?.StartsWith("/works/", StringComparison.OrdinalIgnoreCase) == true
            ? workKey["/works/".Length..]
            : null;
        var coverUri = CreateCoverUri(coverId);
        if (coverUri is not null &&
            !await CoverImageValidator.IsValidAsync(httpClient, coverUri, cancellationToken))
        {
            coverUri = null;
        }

        return new BookMetadataResult(
            isbn13.Value,
            ReadString(documentElement, "title"),
            ReadStrings(documentElement, "author_name"),
            ReadStrings(documentElement, "publisher"),
            ReadInt(documentElement, "first_publish_year"),
            coverUri,
            "OpenLibrary",
            workId,
            DateTimeOffset.UtcNow,
            coverUri is null ? null : "OpenLibrary");
    }

    private Uri BuildRequestUri(Isbn13 isbn13)
    {
        var fields = Uri.EscapeDataString(
            "title,author_name,publisher,first_publish_year,cover_i,key");
        return new Uri(
            $"{_options.OpenLibrarySearchEndpoint}?isbn={isbn13.Value}&limit=1&fields={fields}",
            UriKind.Absolute);
    }

    private Uri? CreateCoverUri(int? coverId)
    {
        return coverId is null ||
               !Uri.TryCreate(
                   string.Format(CultureInfo.InvariantCulture, _options.OpenLibraryCoverEndpoint, coverId),
                   UriKind.Absolute,
                   out var coverUri)
            ? null
            : coverUri;
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

    private static int? ReadInt(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var property))
        {
            return null;
        }

        if (property.ValueKind == JsonValueKind.Number && property.TryGetInt32(out var number))
        {
            return number;
        }

        return property.ValueKind == JsonValueKind.String &&
               int.TryParse(property.GetString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out number)
            ? number
            : null;
    }
}
