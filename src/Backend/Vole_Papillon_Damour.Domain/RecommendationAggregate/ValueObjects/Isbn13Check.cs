using Vole_Papillon_Damour.Domain.BookAggregate.ValueObjects;

namespace Vole_Papillon_Damour.Domain.RecommendationAggregate.ValueObjects;

public static class Isbn13Check
{
    public static bool IsCanonical(string? value) =>
        value is { Length: 13 } && Isbn13.TryCreate(value, out var isbn) && isbn.Value == value;
}
