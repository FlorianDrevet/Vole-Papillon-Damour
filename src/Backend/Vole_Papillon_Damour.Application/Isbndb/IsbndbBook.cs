using System.Text.Json;
using System.Text.Json.Serialization;
using BookIsbn13 = Vole_Papillon_Damour.Domain.BookAggregate.ValueObjects.Isbn13;

namespace Vole_Papillon_Damour.Application.Isbndb;

public sealed record IsbndbBook
{
    [JsonPropertyName("title")]
    public string? Title { get; init; }

    [JsonPropertyName("title_long")]
    public string? TitleLong { get; init; }

    [JsonPropertyName("isbn")]
    public string? Isbn { get; init; }

    [JsonPropertyName("isbn13")]
    public string? Isbn13 { get; init; }

    [JsonPropertyName("isbn10")]
    public string? Isbn10 { get; init; }

    [JsonPropertyName("binding")]
    public string? Binding { get; init; }

    [JsonPropertyName("publisher")]
    public string? Publisher { get; init; }

    [JsonPropertyName("language")]
    public string? Language { get; init; }

    [JsonPropertyName("date_published")]
    public string? DatePublished { get; init; }

    [JsonPropertyName("edition")]
    public string? Edition { get; init; }

    [JsonPropertyName("edition_number")]
    public int? EditionNumber { get; init; }

    [JsonPropertyName("pages")]
    public int? Pages { get; init; }

    [JsonPropertyName("dimensions")]
    public string? Dimensions { get; init; }

    [JsonPropertyName("dimensions_structured")]
    public JsonElement? DimensionsStructured { get; init; }

    [JsonPropertyName("image")]
    public string? Image { get; init; }

    [JsonPropertyName("msrp")]
    public decimal? Msrp { get; init; }

    [JsonPropertyName("list_price")]
    public IsbndbPrice? ListPrice { get; init; }

    [JsonPropertyName("synopsis")]
    public string? Synopsis { get; init; }

    [JsonPropertyName("overview")]
    public string? Overview { get; init; }

    [JsonPropertyName("excerpt")]
    public string? Excerpt { get; init; }

    [JsonPropertyName("dewey_decimal")]
    public string? DeweyDecimal { get; init; }

    [JsonPropertyName("authors")]
    public IReadOnlyList<string> Authors { get; init; } = [];

    [JsonPropertyName("subjects")]
    public IReadOnlyList<string> Subjects { get; init; } = [];

    [JsonPropertyName("other_isbns")]
    public IReadOnlyList<string> OtherIsbns { get; init; } = [];

    [JsonPropertyName("related")]
    public JsonElement? Related { get; init; }

    [JsonIgnore]
    public string RawJson { get; init; } = "{}";

    public bool TryGetIsbn13(out BookIsbn13 isbn13) =>
        BookIsbn13.TryCreate(Isbn13 ?? Isbn, out isbn13);
}

public sealed record IsbndbPrice
{
    [JsonPropertyName("amount")]
    public decimal? Amount { get; init; }

    [JsonPropertyName("currency")]
    public string? Currency { get; init; }
}
