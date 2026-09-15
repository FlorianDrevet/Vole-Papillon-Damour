using System.Xml.Linq;
using Microsoft.Extensions.Options;
using Vole_Papillon_Damour.Application.Books.Common;
using Vole_Papillon_Damour.Application.Common.Interfaces.Services;
using Vole_Papillon_Damour.Domain.BookAggregate.ValueObjects;

namespace Vole_Papillon_Damour.Infrastructure.Services.Bibliographic;

public sealed class BnfSruSearchClient(
    HttpClient httpClient,
    IOptions<BibliographicOptions> options) : IBnfSruSearchClient
{
    private const string Source = "BnF";

    private readonly BibliographicOptions _options = options.Value;

    public async Task<IReadOnlyList<BookReferenceSearchItem>> SearchAsync(
        string query,
        int page,
        int pageSize,
        CancellationToken cancellationToken)
    {
        using var response = await httpClient.GetAsync(
            BuildRequestUri(query, page, pageSize),
            HttpCompletionOption.ResponseHeadersRead,
            cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            return [];
        }

        await using var responseStream = await response.Content.ReadAsStreamAsync(cancellationToken);
        var document = await XDocument.LoadAsync(
            responseStream,
            LoadOptions.None,
            cancellationToken);

        var results = new List<BookReferenceSearchItem>();
        foreach (var recordData in document
                     .Descendants()
                     .Where(element => element.Name.LocalName == "recordData"))
        {
            var dataFields = recordData
                .Descendants()
                .Where(element => element.Name.LocalName == "datafield")
                .ToArray();
            var isbn13 = ReadIsbn13(dataFields);
            var title = BnfSruClient.Clean(BnfSruClient.FirstSubfield(dataFields, ["200"], "a"));
            if (isbn13 is null || title is null)
            {
                continue;
            }

            results.Add(new BookReferenceSearchItem(
                isbn13,
                null,
                title,
                BnfSruClient.Clean(BnfSruClient.ReadAuthors(dataFields)),
                BnfSruClient.Clean(BnfSruClient.FirstSubfield(dataFields, ["210", "214"], "c")),
                BnfSruClient.ParseYear(BnfSruClient.FirstSubfield(dataFields, ["210", "214"], "d")),
                null,
                Source));
        }

        return results
            .GroupBy(item => item.Isbn13, StringComparer.Ordinal)
            .Select(group => group.First())
            .ToArray();
    }

    private Uri BuildRequestUri(string query, int page, int pageSize)
    {
        var cql = Isbn13.TryCreate(query, out var isbn13)
            ? $"bib.isbn all \"{isbn13.Value}\""
            : $"bib.anywhere all \"{SanitizeCqlTerm(query)}\"";
        var requestQuery = string.Join(
            "&",
            "version=1.2",
            "operation=searchRetrieve",
            $"query={Uri.EscapeDataString(cql)}",
            "recordSchema=unimarcXchange",
            $"maximumRecords={pageSize}",
            $"startRecord={((page - 1) * pageSize) + 1}");

        return new Uri($"{_options.BnfSruEndpoint}?{requestQuery}", UriKind.Absolute);
    }

    private static string? ReadIsbn13(IEnumerable<XElement> dataFields)
    {
        return dataFields
            .Where(field => field.Attribute("tag")?.Value == "010")
            .Select(field => BnfSruClient.Subfield(field, "a"))
            .Select(value => Isbn13.TryCreate(value, out var isbn) ? isbn.Value : null)
            .FirstOrDefault(value => value is not null);
    }

    private static string SanitizeCqlTerm(string query)
    {
        return string.Join(
            ' ',
            query.Split(['"', '\\'], StringSplitOptions.RemoveEmptyEntries)
                .SelectMany(part => part.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)));
    }
}
