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
    private const int MaximumWatchlistPublisherLength = 200;

    private readonly BibliographicOptions _options = options.Value;

    public async Task<BookMetadataResult?> FindAsync(
        Isbn13 isbn13,
        CancellationToken cancellationToken,
        bool includeBibliographicDetails = false)
    {
        var edition = includeBibliographicDetails
            ? await ReadEditionDetailsAsync(isbn13, cancellationToken)
            : OpenLibraryEditionDetails.Empty;
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
            if (!includeBibliographicDetails)
            {
                edition = await ReadEditionDetailsAsync(isbn13, cancellationToken);
            }

            return edition.IsEmpty
                ? null
                : new BookMetadataResult(
                    isbn13.Value,
                    edition.Title,
                    null,
                    null,
                    null,
                    null,
                    "OpenLibrary",
                    edition.WorkId,
                    DateTimeOffset.UtcNow,
                    Summary: BookSummaryText.Clean(edition.Summary, edition.Title),
                    PhysicalFormat: Clean(edition.PhysicalFormat),
                    Language: Clean(edition.Language));
        }

        var documentElement = documents[0];
        var coverId = ReadInt(documentElement, "cover_i");
        var workKey = ReadString(documentElement, "key");
        var workId = workKey?.StartsWith("/works/", StringComparison.OrdinalIgnoreCase) == true
            ? workKey["/works/".Length..]
            : null;
        if (!includeBibliographicDetails && string.IsNullOrWhiteSpace(workId))
        {
            edition = await ReadEditionDetailsAsync(isbn13, cancellationToken);
            workId = edition.WorkId;
        }
        var genres = ReadStringValues(documentElement, "subject")
            .Concat(ReadStringValues(documentElement, "subject_facet"))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
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
            coverUri is null ? null : "OpenLibrary",
            Genre: genres.FirstOrDefault(),
            Genres: genres,
            Summary: BookSummaryText.Clean(edition.Summary, ReadString(documentElement, "title"))
                    ?? BookSummaryText.Clean(ReadString(documentElement, "description"), ReadString(documentElement, "title")),
            PhysicalFormat: Clean(edition.PhysicalFormat ?? ReadString(documentElement, "physical_format")),
            Language: Clean(edition.Language ?? ReadFirstString(documentElement, "language")));
    }

    private async Task<OpenLibraryEditionDetails> ReadEditionDetailsAsync(
        Isbn13 isbn13,
        CancellationToken cancellationToken)
    {
        try
        {
            using var response = await httpClient.GetAsync(
                BuildEditionRequestUri(isbn13),
                HttpCompletionOption.ResponseHeadersRead,
                cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                return OpenLibraryEditionDetails.Empty;
            }

            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
            var root = document.RootElement;
            var workId = ReadFirstWorkId(root);
            var title = ReadString(root, "title");
            var summary = ReadDescription(root);
            if (summary is null && workId is not null)
            {
                summary = await ReadWorkDescriptionAsync(workId, cancellationToken);
            }

            return new OpenLibraryEditionDetails(
                title,
                workId,
                summary,
                ReadString(root, "physical_format"),
                ReadFirstLanguage(root));
        }
        catch (HttpRequestException)
        {
            return OpenLibraryEditionDetails.Empty;
        }
        catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return OpenLibraryEditionDetails.Empty;
        }
        catch (JsonException)
        {
            return OpenLibraryEditionDetails.Empty;
        }
    }

    private async Task<string?> ReadWorkDescriptionAsync(
        string workId,
        CancellationToken cancellationToken)
    {
        try
        {
            using var response = await httpClient.GetAsync(
                BuildWorkRequestUri(workId),
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

    private Uri BuildRequestUri(Isbn13 isbn13)
    {
        var fields = Uri.EscapeDataString(
            "title,author_name,publisher,first_publish_year,cover_i,key,subject,subject_facet");
        return new Uri(
            $"{_options.OpenLibrarySearchEndpoint}?isbn={isbn13.Value}&limit=1&fields={fields}",
            UriKind.Absolute);
    }

    private Uri BuildEditionRequestUri(Isbn13 isbn13)
    {
        var endpoint = new Uri(_options.OpenLibrarySearchEndpoint, UriKind.Absolute);
        return new Uri(
            $"{endpoint.GetLeftPart(UriPartial.Authority)}/isbn/{isbn13.Value}.json",
            UriKind.Absolute);
    }

    private Uri BuildWorkRequestUri(string workId)
    {
        var endpoint = new Uri(_options.OpenLibrarySearchEndpoint, UriKind.Absolute);
        return new Uri(
            $"{endpoint.GetLeftPart(UriPartial.Authority)}/works/{Uri.EscapeDataString(workId)}.json",
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

    private static string? ReadDescription(JsonElement element)
    {
        if (!element.TryGetProperty("description", out var description))
        {
            return null;
        }

        return description.ValueKind == JsonValueKind.String
            ? description.GetString()
            : description.ValueKind == JsonValueKind.Object &&
              description.TryGetProperty("value", out var value) &&
              value.ValueKind == JsonValueKind.String
                ? value.GetString()
                : null;
    }

    private static string? ReadFirstWorkId(JsonElement edition)
    {
        if (!edition.TryGetProperty("works", out var works) || works.ValueKind != JsonValueKind.Array)
        {
            return null;
        }

        foreach (var work in works.EnumerateArray())
        {
            var key = ReadString(work, "key");
            if (key?.StartsWith("/works/", StringComparison.OrdinalIgnoreCase) == true)
            {
                return key["/works/".Length..];
            }
        }

        return null;
    }

    private static string? ReadFirstLanguage(JsonElement edition)
    {
        if (!edition.TryGetProperty("languages", out var languages) ||
            languages.ValueKind != JsonValueKind.Array)
        {
            return null;
        }

        foreach (var language in languages.EnumerateArray())
        {
            var key = language.ValueKind == JsonValueKind.String
                ? language.GetString()
                : ReadString(language, "key");
            if (string.IsNullOrWhiteSpace(key))
            {
                continue;
            }

            var value = key.Trim();
            var separator = value.LastIndexOf('/');
            return separator >= 0 ? value[(separator + 1)..] : value;
        }

        return null;
    }

    private static string? ReadFirstString(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var property) ||
            property.ValueKind != JsonValueKind.Array)
        {
            return null;
        }

        return property
            .EnumerateArray()
            .Where(item => item.ValueKind == JsonValueKind.String)
            .Select(item => item.GetString())
            .FirstOrDefault(value => !string.IsNullOrWhiteSpace(value))?
            .Trim();
    }

    private static IReadOnlyList<string> ReadStringValues(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var property) ||
            property.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        return property
            .EnumerateArray()
            .Where(item => item.ValueKind == JsonValueKind.String)
            .Select(item => item.GetString())
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value!.Trim())
            .ToArray();
    }

    public async Task<IReadOnlyList<BookReferenceSearchItem>> SearchAsync(
        string query,
        int page,
        int pageSize,
        CancellationToken cancellationToken)
    {
        var fields = Uri.EscapeDataString(
            "title,author_name,publisher,first_publish_year,cover_i,key,isbn");
        var requestUri = new Uri(
            $"{_options.OpenLibrarySearchEndpoint}?q={Uri.EscapeDataString(query)}&page={page}&limit={pageSize}&fields={fields}",
            UriKind.Absolute);
        using var response = await httpClient.GetAsync(
            requestUri,
            HttpCompletionOption.ResponseHeadersRead,
            cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            return [];
        }

        await using var responseStream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var document = await JsonDocument.ParseAsync(responseStream, cancellationToken: cancellationToken);
        if (!document.RootElement.TryGetProperty("docs", out var documents) ||
            documents.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        var results = new List<BookReferenceSearchItem>();
        foreach (var documentElement in documents.EnumerateArray())
        {
            var workKey = ReadString(documentElement, "key");
            var workId = workKey?.StartsWith("/works/", StringComparison.OrdinalIgnoreCase) == true
                ? workKey["/works/".Length..]
                : null;
            var isbn13 = ReadStrings(documentElement, "isbn")
                ?.Split(", ", StringSplitOptions.RemoveEmptyEntries)
                .Select(value => Isbn13.TryCreate(value, out var isbn) ? isbn.Value : null)
                .FirstOrDefault(value => value is not null);
            if (isbn13 is null && workId is null)
            {
                continue;
            }

            var coverId = ReadInt(documentElement, "cover_i");
            // Open Library may aggregate publisher names across editions. Keep an oversized
            // aggregate out of the optional watchlist snapshot instead of rejecting the follow.
            var publisher = ReadStrings(documentElement, "publisher");
            if (publisher?.Length > MaximumWatchlistPublisherLength)
            {
                publisher = null;
            }

            results.Add(new BookReferenceSearchItem(
                isbn13,
                workId,
                ReadString(documentElement, "title"),
                ReadStrings(documentElement, "author_name"),
                publisher,
                ReadInt(documentElement, "first_publish_year"),
                CreateCoverUri(coverId),
                "OpenLibrary"));
        }

        return results
            .GroupBy(item => $"{item.Isbn13}|{item.WorkId}", StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .ToArray();
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

    private static string? Clean(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    private sealed record OpenLibraryEditionDetails(
        string? Title,
        string? WorkId,
        string? Summary,
        string? PhysicalFormat,
        string? Language)
    {
        public static OpenLibraryEditionDetails Empty { get; } = new(null, null, null, null, null);
        public bool IsEmpty => Title is null && WorkId is null && Summary is null && PhysicalFormat is null && Language is null;
    }
}
