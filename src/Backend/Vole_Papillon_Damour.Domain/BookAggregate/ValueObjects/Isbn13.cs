namespace Vole_Papillon_Damour.Domain.BookAggregate.ValueObjects;

public readonly record struct Isbn13
{
    private Isbn13(string value)
    {
        Value = value;
    }

    public string Value { get; }

    public static bool TryCreate(string? input, out Isbn13 isbn13)
    {
        isbn13 = default;

        if (string.IsNullOrWhiteSpace(input))
        {
            return false;
        }

        var normalized = RemoveSeparators(input);

        if (normalized.Length == 10 && IsValidIsbn10(normalized))
        {
            var isbn13Digits = $"978{normalized[..9]}";
            isbn13 = new Isbn13($"{isbn13Digits}{CalculateIsbn13CheckDigit(isbn13Digits)}");
            return true;
        }

        if (normalized.Length == 13 && IsValidIsbn13(normalized))
        {
            isbn13 = new Isbn13(normalized);
            return true;
        }

        return false;
    }

    public override string ToString() => Value;

    private static string RemoveSeparators(string input)
    {
        return string.Concat(input.Where(character => !char.IsWhiteSpace(character) && character != '-'));
    }

    private static bool IsValidIsbn10(string value)
    {
        for (var index = 0; index < 9; index++)
        {
            if (!IsAsciiDigit(value[index]))
            {
                return false;
            }
        }

        var checkDigit = value[9] switch
        {
            >= '0' and <= '9' => value[9] - '0',
            'X' or 'x' => 10,
            _ => -1
        };

        if (checkDigit < 0)
        {
            return false;
        }

        var sum = checkDigit;
        for (var index = 0; index < 9; index++)
        {
            sum += (10 - index) * (value[index] - '0');
        }

        return sum % 11 == 0;
    }

    private static bool IsValidIsbn13(string value)
    {
        if (!value.StartsWith("978", StringComparison.Ordinal) &&
            !value.StartsWith("979", StringComparison.Ordinal))
        {
            return false;
        }

        if (value.Any(character => !IsAsciiDigit(character)))
        {
            return false;
        }

        var expectedCheckDigit = CalculateIsbn13CheckDigit(value[..12]);
        return value[12] - '0' == expectedCheckDigit;
    }

    private static int CalculateIsbn13CheckDigit(string firstTwelveDigits)
    {
        var sum = 0;
        for (var index = 0; index < firstTwelveDigits.Length; index++)
        {
            sum += (firstTwelveDigits[index] - '0') * (index % 2 == 0 ? 1 : 3);
        }

        return (10 - sum % 10) % 10;
    }

    private static bool IsAsciiDigit(char character) => character is >= '0' and <= '9';
}
