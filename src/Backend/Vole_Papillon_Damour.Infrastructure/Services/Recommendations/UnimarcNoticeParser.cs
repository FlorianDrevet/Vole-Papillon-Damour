using System.Globalization;
using System.Xml.Linq;
using Vole_Papillon_Damour.Application.Recommendations.Similarity;
using Vole_Papillon_Damour.Infrastructure.Services.Bibliographic;

namespace Vole_Papillon_Damour.Infrastructure.Services.Recommendations;

public static class UnimarcNoticeParser
{
    public static SimilarityEdition Parse(XElement record, string isbn13)
    {
        ArgumentNullException.ThrowIfNull(record);
        ArgumentException.ThrowIfNullOrWhiteSpace(isbn13);

        var dataFields = record
            .DescendantsAndSelf()
            .Where(element => element.Name.LocalName == "datafield")
            .ToArray();
        var authors = new List<string>();
        var surnames = new List<string>();
        foreach (var field in dataFields.Where(field => field.Attribute("tag")?.Value is "700" or "701" or "702"))
        {
            var tag = field.Attribute("tag")?.Value;
            var role = BnfSruClient.Subfield(field, "4");
            if (tag == "702" && role is not null && role != "070")
            {
                continue;
            }

            var surname = Clean(BnfSruClient.Subfield(field, "a"));
            if (surname is null)
            {
                continue;
            }

            var givenName = Clean(BnfSruClient.Subfield(field, "b"));
            authors.Add(givenName is null ? surname : $"{givenName} {surname}");
            surnames.Add(surname);
        }

        var seriesNumber = ParseInteger(BnfSruClient.FirstSubfield(dataFields, ["461"], "v"));
        var partNumber = ParseInteger(BnfSruClient.FirstSubfield(dataFields, ["200"], "i"));
        var forms = dataFields
            .Where(field => field.Attribute("tag")?.Value == "608")
            .Select(field => Clean(BnfSruClient.Subfield(field, "a")))
            .OfType<string>()
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var subjects = dataFields
            .Where(field => field.Attribute("tag")?.Value == "606")
            .SelectMany(field => field
                .Elements()
                .Where(element => element.Name.LocalName == "subfield" &&
                                  element.Attribute("code")?.Value is "a" or "x")
                .Select(element => Clean(element.Value)))
            .OfType<string>()
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var cnljReviewed = dataFields.Any(field => field
            .Elements()
            .Any(element => element.Name.LocalName == "subfield" &&
                            element.Attribute("code")?.Value == "2" &&
                            string.Equals(element.Value.Trim(), "CNLJ", StringComparison.OrdinalIgnoreCase)));
        var languages = dataFields
            .Where(field => field.Attribute("tag")?.Value == "101")
            .Select(field => Clean(BnfSruClient.Subfield(field, "a")))
            .OfType<string>()
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        return new SimilarityEdition(
            isbn13,
            Clean(BnfSruClient.FirstSubfield(dataFields, ["200"], "a")) ?? string.Empty,
            Clean(BnfSruClient.FirstSubfield(dataFields, ["200"], "h")),
            partNumber,
            Clean(BnfSruClient.FirstSubfield(dataFields, ["200"], "e")),
            authors.Distinct(StringComparer.OrdinalIgnoreCase).ToArray(),
            surnames.Distinct(StringComparer.OrdinalIgnoreCase).ToArray(),
            Clean(BnfSruClient.FirstSubfield(dataFields, ["210", "214"], "c")),
            Clean(BnfSruClient.FirstSubfield(dataFields, ["225"], "a")),
            Clean(BnfSruClient.FirstSubfield(dataFields, ["461"], "t")),
            seriesNumber,
            Clean(BnfSruClient.FirstSubfield(dataFields, ["500"], "3")),
            null,
            forms,
            subjects,
            Clean(BnfSruClient.FirstSubfield(dataFields, ["333"], "a")),
            cnljReviewed,
            Clean(BnfSruClient.FirstSubfield(dataFields, ["676"], "a")),
            Clean(BnfSruClient.FirstSubfield(dataFields, ["330"], "a")),
            null,
            languages);
    }

    private static int? ParseInteger(string? value) =>
        int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed) ? parsed : null;

    private static string? Clean(string? value) =>
        string.IsNullOrWhiteSpace(value)
            ? null
            : string.Join(' ', value.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
}
