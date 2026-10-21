using System.Globalization;
using System.Xml.Linq;
using Microsoft.Extensions.Options;
using Vole_Papillon_Damour.Application.Books.Common;
using Vole_Papillon_Damour.Application.Common.Interfaces.Services;
using Vole_Papillon_Damour.Domain.BookAggregate.ValueObjects;

namespace Vole_Papillon_Damour.Infrastructure.Services.Bibliographic;

public sealed class BnfSruClient(
    HttpClient httpClient,
    IOptions<BibliographicOptions> options) : IBnfSruClient
{
    private readonly BibliographicOptions _options = options.Value;

    public async Task<BookMetadataResult?> FindAsync(
        Isbn13 isbn13,
        CancellationToken cancellationToken)
    {
        var requestUri = BuildRequestUri(isbn13);
        using var response = await httpClient.GetAsync(
            requestUri,
            HttpCompletionOption.ResponseHeadersRead,
            cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            return null;
        }

        await using var responseStream = await response.Content.ReadAsStreamAsync(cancellationToken);
        var document = await XDocument.LoadAsync(
            responseStream,
            LoadOptions.None,
            cancellationToken);
        var recordData = document
            .Descendants()
            .FirstOrDefault(element => element.Name.LocalName == "recordData");

        if (recordData is null)
        {
            return null;
        }

        var dataFields = recordData
            .Descendants()
            .Where(element => element.Name.LocalName == "datafield")
            .ToArray();
        var title = FirstSubfield(dataFields, ["200"], "a")
            ?? FirstElementValue(recordData, "title");
        var authors = ReadAuthors(dataFields)
            ?? FirstElementValue(recordData, "creator");
        var publisher = FirstSubfield(dataFields, ["210", "214"], "c")
            ?? FirstElementValue(recordData, "publisher");
        var publicationYear = ParseYear(
            FirstSubfield(dataFields, ["210", "214"], "d")
            ?? FirstElementValue(recordData, "date"));
        var genres = ReadGenres(dataFields, recordData);
        var coverUri = CreateCoverUri(isbn13);
        if (coverUri is not null &&
            !await CoverImageValidator.IsValidAsync(httpClient, coverUri, cancellationToken))
        {
            coverUri = null;
        }

        return new BookMetadataResult(
            isbn13.Value,
            Clean(title),
            Clean(authors),
            Clean(publisher),
            publicationYear,
            coverUri,
            "BnF",
            null,
            DateTimeOffset.UtcNow,
            coverUri is null ? null : "BnF",
            Genre: genres.FirstOrDefault(),
            Genres: genres,
            Summary: BookSummaryText.Clean(FirstSubfield(dataFields, ["330"], "a"), Clean(title)),
            Language: Clean(FirstSubfield(dataFields, ["101"], "a")));
    }

    private Uri BuildRequestUri(Isbn13 isbn13)
    {
        var query = string.Join(
            "&",
            "version=1.2",
            "operation=searchRetrieve",
            $"query={Uri.EscapeDataString(IsbnQuery(isbn13))}",
            "recordSchema=unimarcXchange",
            "maximumRecords=1",
            "startRecord=1");

        return new Uri($"{_options.BnfSruEndpoint}?{query}", UriKind.Absolute);
    }

    // bib.isbn misses notices recorded with the ISBN-10 of pre-2007 editions when
    // queried with the normalized ISBN-13; bib.fuzzyISBN matches both forms.
    internal static string IsbnQuery(Isbn13 isbn13)
    {
        return $"bib.fuzzyISBN all \"{isbn13.Value}\"";
    }

    internal static string? ReadAuthors(IEnumerable<XElement> dataFields)
    {
        var authors = dataFields
            .Where(field => field.Attribute("tag")?.Value is "700" or "701" or "702")
            .Select(field =>
            {
                var familyName = Subfield(field, "a");
                var givenName = Subfield(field, "b");
                return string.IsNullOrWhiteSpace(givenName)
                    ? familyName
                    : $"{familyName}, {givenName}";
            })
            .Where(author => !string.IsNullOrWhiteSpace(author))
            .Select(author => author!.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        return authors.Length == 0 ? null : string.Join(", ", authors);
    }

    internal static string? FirstSubfield(
        IEnumerable<XElement> dataFields,
        IEnumerable<string> tags,
        string code)
    {
        var tagSet = tags.ToHashSet(StringComparer.Ordinal);
        return dataFields
            .Where(field => tagSet.Contains(field.Attribute("tag")?.Value ?? string.Empty))
            .Select(field => Subfield(field, code))
            .FirstOrDefault(value => !string.IsNullOrWhiteSpace(value));
    }

    internal static IReadOnlyList<string> ReadGenres(
        IEnumerable<XElement> dataFields,
        XElement recordData)
    {
        var genreTags = new HashSet<string>(["608", "606", "610"], StringComparer.Ordinal);
        var values = dataFields
            .Where(field => genreTags.Contains(field.Attribute("tag")?.Value ?? string.Empty))
            .SelectMany(field => field.Elements()
                .Where(element => element.Name.LocalName == "subfield" &&
                                  string.Equals(element.Attribute("code")?.Value, "a", StringComparison.Ordinal))
                .Select(element => new
                {
                    Priority = field.Attribute("tag")?.Value == "608" ? 0 : 1,
                    Value = element.Value
                }))
            .OrderBy(item => item.Priority)
            .Select(item => item.Value)
            .Concat(recordData.Descendants()
                .Where(element => element.Name.LocalName is "genre" or "subject")
                .OrderBy(element => element.Name.LocalName == "genre" ? 0 : 1)
                .Select(element => element.Value));

        return values
            .Select(Clean)
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value!)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    internal static string? Subfield(XElement dataField, string code)
    {
        return dataField
            .Elements()
            .FirstOrDefault(element =>
                element.Name.LocalName == "subfield" &&
                string.Equals(element.Attribute("code")?.Value, code, StringComparison.Ordinal))
            ?.Value;
    }

    private static string? FirstElementValue(XElement parent, string localName)
    {
        return parent
            .Descendants()
            .FirstOrDefault(element => element.Name.LocalName == localName)
            ?.Value;
    }

    internal static int? ParseYear(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        for (var index = 0; index <= value.Length - 4; index++)
        {
            var candidate = value.Substring(index, 4);
            if (candidate[0] is not ('1' or '2') || !int.TryParse(
                    candidate,
                    NumberStyles.None,
                    CultureInfo.InvariantCulture,
                    out var year))
            {
                continue;
            }

            return year;
        }

        return null;
    }

    internal static string? Clean(string? value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? null
            : string.Join(' ', value.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
    }

    private static Uri? CreateCoverUri(Isbn13 isbn13)
    {
        return Uri.TryCreate(
            $"https://openapi.bnf.fr/couverture/image/image/recupererImage?ISBN={isbn13.Value}&couverture=1",
            UriKind.Absolute,
            out var coverUri)
            ? coverUri
            : null;
    }
}
