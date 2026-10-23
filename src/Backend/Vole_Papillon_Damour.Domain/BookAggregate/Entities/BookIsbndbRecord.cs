using Vole_Papillon_Damour.Domain.BookAggregate.ValueObjects;
using Vole_Papillon_Damour.Domain.Common;

namespace Vole_Papillon_Damour.Domain.BookAggregate.Entities;

public sealed class BookIsbndbRecord
{
    public Isbn13 Isbn13 { get; init; }
    public string? TitleLong { get; init; }
    public string? Isbn10 { get; init; }
    public string? Publisher { get; init; }
    public string? Binding { get; init; }
    public string? DatePublished { get; init; }
    public string? Edition { get; init; }
    public int? EditionNumber { get; init; }
    public string? Dimensions { get; init; }
    public decimal? HeightValue { get; init; }
    public string? HeightUnit { get; init; }
    public decimal? WidthValue { get; init; }
    public string? WidthUnit { get; init; }
    public decimal? LengthValue { get; init; }
    public string? LengthUnit { get; init; }
    public decimal? WeightValue { get; init; }
    public string? WeightUnit { get; init; }
    public string? DeweyDecimal { get; init; }
    public string? Synopsis { get; init; }
    public string? Overview { get; init; }
    public string? Excerpt { get; init; }
    public string? Subjects { get; init; }
    public string? Authors { get; init; }
    public string? OtherIsbns { get; init; }
    public string? Related { get; init; }
    public decimal? Msrp { get; init; }
    public decimal? ListPriceAmount { get; init; }
    public string? ListPriceCurrency { get; init; }
    public string? LanguageCode { get; init; }
    public string? ImageUrl { get; init; }
    public string? RawJson { get; init; }
    public DateTime FetchedAt { get; init; }

    public static BookIsbndbRecord Create(
        Isbn13 isbn13,
        DateTime fetchedAt,
        string? titleLong = null,
        string? isbn10 = null,
        string? publisher = null,
        string? binding = null,
        string? datePublished = null,
        string? edition = null,
        int? editionNumber = null,
        string? dimensions = null,
        decimal? heightValue = null,
        string? heightUnit = null,
        decimal? widthValue = null,
        string? widthUnit = null,
        decimal? lengthValue = null,
        string? lengthUnit = null,
        decimal? weightValue = null,
        string? weightUnit = null,
        string? deweyDecimal = null,
        string? synopsis = null,
        string? overview = null,
        string? excerpt = null,
        string? subjects = null,
        string? authors = null,
        string? otherIsbns = null,
        string? related = null,
        decimal? msrp = null,
        decimal? listPriceAmount = null,
        string? listPriceCurrency = null,
        string? languageCode = null,
        string? imageUrl = null,
        string? rawJson = null)
    {
        return new BookIsbndbRecord
        {
            Isbn13 = isbn13,
            FetchedAt = DomainTime.RequireUtc(fetchedAt, nameof(fetchedAt)),
            TitleLong = titleLong,
            Isbn10 = isbn10,
            Publisher = publisher,
            Binding = binding,
            DatePublished = datePublished,
            Edition = edition,
            EditionNumber = editionNumber,
            Dimensions = dimensions,
            HeightValue = heightValue,
            HeightUnit = heightUnit,
            WidthValue = widthValue,
            WidthUnit = widthUnit,
            LengthValue = lengthValue,
            LengthUnit = lengthUnit,
            WeightValue = weightValue,
            WeightUnit = weightUnit,
            DeweyDecimal = deweyDecimal,
            Synopsis = synopsis,
            Overview = overview,
            Excerpt = excerpt,
            Subjects = subjects,
            Authors = authors,
            OtherIsbns = otherIsbns,
            Related = related,
            Msrp = msrp,
            ListPriceAmount = listPriceAmount,
            ListPriceCurrency = listPriceCurrency,
            LanguageCode = languageCode,
            ImageUrl = imageUrl,
            RawJson = rawJson
        };
    }

    public BookIsbndbRecord()
    {
    }
}
