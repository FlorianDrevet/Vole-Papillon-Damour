using System.Net;
using System.Text.Json;
using System.Xml.Linq;
using Microsoft.Extensions.Options;
using Vole_Papillon_Damour.Application.Common.Interfaces.Services;
using Vole_Papillon_Damour.Application.Recommendations.Similarity;
using Vole_Papillon_Damour.Infrastructure.Services.Bibliographic;

namespace Vole_Papillon_Damour.Infrastructure.Services.Recommendations;

public sealed class BibliographicNoticeReader(
    HttpClient httpClient,
    IOptions<BibliographicOptions> options) : IBibliographicNoticeReader
{
    private readonly BibliographicOptions _options = options.Value;

    public async Task<SimilarityEdition?> ReadAsync(string isbn13, CancellationToken cancellationToken)
    {
        var bnfEdition = await ReadBnfAsync(isbn13, cancellationToken);
        var openLibraryEdition = await ReadOpenLibraryAsync(isbn13, cancellationToken);

        if (bnfEdition is null)
        {
            return openLibraryEdition;
        }

        return openLibraryEdition is null
            ? bnfEdition
            : bnfEdition with
            {
                OpenLibraryWorkKey = openLibraryEdition.OpenLibraryWorkKey,
                OpenLibraryDescription = openLibraryEdition.OpenLibraryDescription ?? bnfEdition.OpenLibraryDescription,
            };
    }

    private async Task<SimilarityEdition?> ReadBnfAsync(string isbn13, CancellationToken cancellationToken)
    {
        try
        {
            var requestUri = BuildBnfRequestUri(isbn13);
            using var response = await httpClient.GetAsync(
                requestUri,
                HttpCompletionOption.ResponseHeadersRead,
                cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                return null;
            }

            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            var document = await XDocument.LoadAsync(stream, LoadOptions.None, cancellationToken);
            var recordData = document.Descendants().FirstOrDefault(element => element.Name.LocalName == "recordData");
            var record = recordData?.Descendants().FirstOrDefault(element => element.Name.LocalName == "record");
            return record is null ? null : UnimarcNoticeParser.Parse(record, isbn13);
        }
        catch (HttpRequestException)
        {
            return null;
        }
        catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return null;
        }
    }

    private async Task<SimilarityEdition?> ReadOpenLibraryAsync(string isbn13, CancellationToken cancellationToken)
    {
        try
        {
            using var response = await httpClient.GetAsync(
                $"https://openlibrary.org/isbn/{Uri.EscapeDataString(isbn13)}.json",
                HttpCompletionOption.ResponseHeadersRead,
                cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                return null;
            }

            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            using var editionDocument = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
            var root = editionDocument.RootElement;
            var title = ReadString(root, "title");
            var workKey = ReadFirstWorkKey(root);
            var description = ReadDescription(root);

            if (description is null && workKey is not null)
            {
                description = await ReadWorkDescriptionAsync(workKey, cancellationToken);
            }

            if (string.IsNullOrWhiteSpace(title) && workKey is null && description is null)
            {
                return null;
            }

            return new SimilarityEdition(
                isbn13,
                title ?? string.Empty,
                null,
                null,
                null,
                [],
                [],
                null,
                null,
                null,
                null,
                null,
                workKey,
                [],
                [],
                null,
                false,
                null,
                null,
                description,
                []);
        }
        catch (HttpRequestException)
        {
            return null;
        }
        catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private async Task<string?> ReadWorkDescriptionAsync(string workKey, CancellationToken cancellationToken)
    {
        try
        {
            using var response = await httpClient.GetAsync(
                $"https://openlibrary.org{workKey}.json",
                HttpCompletionOption.ResponseHeadersRead,
                cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                return null;
            }

            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
            return ReadDescription(document.RootElement);
        }
        catch (HttpRequestException)
        {
            return null;
        }
        catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private Uri BuildBnfRequestUri(string isbn13)
    {
        var query = string.Join(
            "&",
            "version=1.2",
            "operation=searchRetrieve",
            $"query={Uri.EscapeDataString($"bib.fuzzyISBN all \"{isbn13}\"")}",
            "recordSchema=unimarcXchange",
            "maximumRecords=1",
            "startRecord=1");
        return new Uri($"{_options.BnfSruEndpoint}?{query}", UriKind.Absolute);
    }

    private static string? ReadFirstWorkKey(JsonElement edition)
    {
        if (!edition.TryGetProperty("works", out var works) || works.ValueKind != JsonValueKind.Array)
        {
            return null;
        }

        foreach (var work in works.EnumerateArray())
        {
            var key = ReadString(work, "key");
            if (!string.IsNullOrWhiteSpace(key))
            {
                return key;
            }
        }

        return null;
    }

    private static string? ReadDescription(JsonElement value) => ReadString(value, "description");

    private static string? ReadString(JsonElement value, string propertyName)
    {
        if (value.ValueKind != JsonValueKind.Object || !value.TryGetProperty(propertyName, out var property))
        {
            return null;
        }

        if (property.ValueKind == JsonValueKind.String)
        {
            return property.GetString();
        }

        return property.ValueKind == JsonValueKind.Object &&
               property.TryGetProperty("value", out var nested) &&
               nested.ValueKind == JsonValueKind.String
            ? nested.GetString()
            : null;
    }
}
