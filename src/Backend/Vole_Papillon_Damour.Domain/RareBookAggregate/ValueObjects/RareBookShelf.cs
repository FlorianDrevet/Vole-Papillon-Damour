using Vole_Papillon_Damour.Domain.Common.Models;

namespace Vole_Papillon_Damour.Domain.RareBookAggregate.ValueObjects;

public sealed class RareBookShelf : ValueObject
{
    public const int MaxLength = 80;
    public const string AncientEditionsLabel = "Éditions anciennes";
    public const string IllustratedLabel = "Illustrés";
    public const string FineArtsLabel = "Beaux-arts";
    public const string RegionalismLabel = "Régionalisme";

    public string Value { get; private set; } = null!;

    public RareBookShelf()
    {
    }

    public RareBookShelf(string value)
    {
        Value = Normalize(value);
    }

    public static RareBookShelf AncientEditions => new(AncientEditionsLabel);
    public static RareBookShelf Illustrated => new(IllustratedLabel);
    public static RareBookShelf FineArts => new(FineArtsLabel);
    public static RareBookShelf Regionalism => new(RegionalismLabel);

    public static RareBookShelf Create(string value) => new(value);

    public override IEnumerable<object> GetEqualityComponents()
    {
        yield return Value;
    }

    public override string ToString() => Value;

    private static string Normalize(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("A rare book shelf label is required.", nameof(value));
        }

        var normalized = value.Trim();
        if (normalized.Length > MaxLength)
        {
            throw new ArgumentException(
                $"A rare book shelf label cannot exceed {MaxLength} characters.",
                nameof(value));
        }

        return normalized;
    }
}
