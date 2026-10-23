using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using Vole_Papillon_Damour.Application.Books.Common;
using Vole_Papillon_Damour.Domain.BookAggregate.Entities;
using Vole_Papillon_Damour.Domain.BookAggregate.ValueObjects;

namespace Vole_Papillon_Damour.Application.Isbndb;

public static class IsbndbNoticeMapper
{
    private static readonly Regex YearPrefix = new(@"\b(\d{4})", RegexOptions.Compiled | RegexOptions.CultureInvariant);

    public static BookMetadataPatch ToPatch(IsbndbBook book)
    {
        ArgumentNullException.ThrowIfNull(book);

        var title = Limit(FirstNotBlank(book.Title, book.TitleLong), 500);
        var authors = Limit(MapAuthors(book.Authors), 500);
        var publisher = Limit(book.Publisher, 200);
        var publicationYear = ParsePublicationYear(book.DatePublished);
        var physicalFormat = Limit(MapPhysicalFormat(book.Binding), 50);
        var language = MapLanguage(book.Language);
        var summary = BookSummaryText.Clean(book.Synopsis, title) ??
                      BookSummaryText.Clean(book.Overview, title);
        var coverUrl = MapCoverUrl(book.Image);
        var pages = book.Pages is > 0 and <= 100_000 ? book.Pages : null;
        var edition = Limit(book.Edition, 100);
        var genreMap = IsbndbGenreMapper.Map(book.Subjects, publisher, book.Authors);
        var hasGenreNotice = book.Subjects is not null || !string.IsNullOrWhiteSpace(publisher);

        var fields = new List<BookMetadataField>();
        AddIfPresent(fields, BookMetadataField.Title, title);
        AddIfPresent(fields, BookMetadataField.Authors, authors);
        AddIfPresent(fields, BookMetadataField.Publisher, publisher);
        if (publicationYear is not null)
        {
            fields.Add(BookMetadataField.PublicationYear);
        }

        AddIfPresent(fields, BookMetadataField.PhysicalFormat, physicalFormat);
        AddIfPresent(fields, BookMetadataField.Language, language);
        AddIfPresent(fields, BookMetadataField.Summary, summary);
        AddIfPresent(fields, BookMetadataField.CoverUrl, coverUrl);
        if (pages is not null)
        {
            fields.Add(BookMetadataField.Pages);
        }

        AddIfPresent(fields, BookMetadataField.Edition, edition);
        if (hasGenreNotice)
        {
            fields.Add(BookMetadataField.Genre);
        }

        return new BookMetadataPatch(
            Title: title,
            Authors: authors,
            Publisher: publisher,
            PublicationYear: publicationYear,
            PhysicalFormat: physicalFormat,
            Language: language,
            Genre: genreMap.Genres.FirstOrDefault(),
            CoverUrl: coverUrl,
            Fields: fields,
            Genres: hasGenreNotice ? genreMap.Genres : null,
            Summary: summary,
            Pages: pages,
            Edition: edition);
    }

    public static BookIsbndbRecord ToRecord(IsbndbBook book, DateTime fetchedAt)
    {
        ArgumentNullException.ThrowIfNull(book);
        if (!book.TryGetIsbn13(out var isbn13))
        {
            throw new ArgumentException("The ISBNdb notice does not contain a valid ISBN-13.", nameof(book));
        }

        var (heightValue, heightUnit) = ReadMeasurement(book.DimensionsStructured, "height");
        var (widthValue, widthUnit) = ReadMeasurement(book.DimensionsStructured, "width");
        var (lengthValue, lengthUnit) = ReadMeasurement(book.DimensionsStructured, "length");
        var (weightValue, weightUnit) = ReadMeasurement(book.DimensionsStructured, "weight");

        return BookIsbndbRecord.Create(
            isbn13,
            fetchedAt,
            titleLong: book.TitleLong,
            isbn10: Limit(book.Isbn10, 10),
            publisher: Limit(book.Publisher, 200),
            binding: Limit(book.Binding, 100),
            datePublished: Limit(book.DatePublished, 50),
            edition: Limit(book.Edition, 100),
            editionNumber: book.EditionNumber,
            dimensions: book.Dimensions,
            heightValue: heightValue,
            heightUnit: heightUnit,
            widthValue: widthValue,
            widthUnit: widthUnit,
            lengthValue: lengthValue,
            lengthUnit: lengthUnit,
            weightValue: weightValue,
            weightUnit: weightUnit,
            deweyDecimal: book.DeweyDecimal,
            synopsis: book.Synopsis,
            overview: book.Overview,
            excerpt: book.Excerpt,
            subjects: Serialize(book.Subjects),
            authors: Serialize(book.Authors),
            otherIsbns: Serialize(book.OtherIsbns),
            related: GetRawJson(book.Related),
            msrp: book.Msrp,
            listPriceAmount: book.ListPrice?.Amount,
            listPriceCurrency: Limit(book.ListPrice?.Currency, 3),
            languageCode: Limit(book.Language, 10),
            imageUrl: Limit(book.Image, 2048),
            rawJson: book.RawJson);
    }

    private static string? MapAuthors(IReadOnlyList<string>? authors)
    {
        if (authors is null)
        {
            return null;
        }

        var normalizedAuthors = authors
            .SelectMany(author => author.Split(';', StringSplitOptions.RemoveEmptyEntries))
            .Select(NormalizeAuthor)
            .Where(author => !string.IsNullOrWhiteSpace(author));
        var result = string.Join(", ", normalizedAuthors);
        return result.Length == 0 ? null : result;
    }

    private static string NormalizeAuthor(string author)
    {
        var value = author.Trim();
        var comma = value.IndexOf(',');
        if (comma < 0)
        {
            return value;
        }

        var surname = value[..comma].Trim();
        var givenNames = value[(comma + 1)..].Trim();
        return surname.Length == 0 ? givenNames :
            givenNames.Length == 0 ? surname : $"{givenNames} {surname}";
    }

    private static int? ParsePublicationYear(string? datePublished)
    {
        var match = YearPrefix.Match(datePublished ?? "");
        return match.Success && int.TryParse(match.Groups[1].Value, NumberStyles.None, CultureInfo.InvariantCulture, out var year)
            ? year is >= 1 and <= 9999 ? year : null
            : null;
    }

    private static string? MapPhysicalFormat(string? binding)
    {
        if (string.IsNullOrWhiteSpace(binding))
        {
            return null;
        }

        var trimmed = binding.Trim();
        return trimmed.ToLowerInvariant() switch
        {
            "mass market paperback" => "Poche",
            "paperback" => "Broché",
            "hardcover" => "Relié",
            "epub" or "ebook" or "kindle edition" => "Numérique",
            "audio cd" => "Livre audio",
            "board book" => "Tout-carton",
            _ => trimmed
        };
    }

    private static string? MapLanguage(string? language)
    {
        if (string.IsNullOrWhiteSpace(language))
        {
            return null;
        }

        var trimmed = language.Trim();
        return trimmed.ToLowerInvariant() switch
        {
            "fra" => "fr",
            "eng" => "en",
            "spa" => "es",
            "deu" or "ger" => "de",
            "ita" => "it",
            _ => trimmed.Length <= 10 ? trimmed : null
        };
    }

    private static string? MapCoverUrl(string? image)
    {
        var value = image?.Trim();
        return value is { Length: <= 2048 } &&
               Uri.TryCreate(value, UriKind.Absolute, out var uri) &&
               uri.Scheme.Equals(Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase)
            ? value
            : null;
    }

    private static (decimal? Value, string? Unit) ReadMeasurement(JsonElement? dimensions, string name)
    {
        if (dimensions is not { ValueKind: JsonValueKind.Object } root ||
            !root.TryGetProperty(name, out var measurement) ||
            measurement.ValueKind != JsonValueKind.Object)
        {
            return (null, null);
        }

        decimal? value = null;
        if (measurement.TryGetProperty("value", out var valueElement))
        {
            if (valueElement.ValueKind == JsonValueKind.Number && valueElement.TryGetDecimal(out var numericValue))
            {
                value = numericValue;
            }
            else if (valueElement.ValueKind == JsonValueKind.String &&
                     decimal.TryParse(valueElement.GetString(), NumberStyles.Number, CultureInfo.InvariantCulture, out numericValue))
            {
                value = numericValue;
            }
        }

        var unit = measurement.TryGetProperty("unit", out var unitElement) && unitElement.ValueKind == JsonValueKind.String
            ? Limit(unitElement.GetString(), 20)
            : null;
        return (value, unit);
    }

    private static string? Serialize<T>(T? value) =>
        value is null ? null : JsonSerializer.Serialize(value);

    private static string? GetRawJson(JsonElement? element) =>
        element is { ValueKind: not JsonValueKind.Undefined and not JsonValueKind.Null } value
            ? value.GetRawText()
            : null;

    private static string? FirstNotBlank(params string?[] values) =>
        values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value))?.Trim();

    private static string? Limit(string? value, int length)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var trimmed = value.Trim();
        return trimmed.Length > length ? trimmed[..length] : trimmed;
    }

    private static void AddIfPresent(List<BookMetadataField> fields, BookMetadataField field, string? value)
    {
        if (!string.IsNullOrWhiteSpace(value))
        {
            fields.Add(field);
        }
    }
}
