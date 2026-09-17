using System.Globalization;
using System.Text;
using Vole_Papillon_Damour.Domain.Common.Models;

namespace Vole_Papillon_Damour.Domain.RareBookAggregate.ValueObjects;

public sealed class RareBookSlug : ValueObject
{
    public const int MaxLength = 120;

    public string Value { get; private set; } = null!;

    public RareBookSlug()
    {
    }

    private RareBookSlug(string value)
    {
        Value = value;
    }

    public static RareBookSlug Create(
        string? title,
        string? authorMention,
        int? publicationYear,
        int collisionSuffix = 1)
    {
        if (collisionSuffix < 1)
        {
            throw new ArgumentOutOfRangeException(
                nameof(collisionSuffix),
                "A slug collision suffix must be positive.");
        }

        var source = string.Join(
            ' ',
            new[]
            {
                title,
                authorMention,
                publicationYear?.ToString(CultureInfo.InvariantCulture)
            }.Where(value => !string.IsNullOrWhiteSpace(value)));

        var slug = Normalize(source);
        if (collisionSuffix > 1)
        {
            slug = AddSuffix(slug, collisionSuffix);
        }

        return new RareBookSlug(slug);
    }

    public static RareBookSlug Create(string value)
    {
        var slug = Normalize(value);
        return new RareBookSlug(slug);
    }

    public RareBookSlug WithCollisionSuffix(int collisionSuffix)
    {
        if (collisionSuffix < 2)
        {
            throw new ArgumentOutOfRangeException(
                nameof(collisionSuffix),
                "A collision suffix must be at least 2.");
        }

        return new RareBookSlug(AddSuffix(Value, collisionSuffix));
    }

    public override IEnumerable<object> GetEqualityComponents()
    {
        yield return Value;
    }

    public override string ToString() => Value;

    private static string Normalize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "livre-rare";
        }

        var decomposed = value.Trim().Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(decomposed.Length);
        var pendingSeparator = false;

        foreach (var character in decomposed)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(character) == UnicodeCategory.NonSpacingMark)
            {
                continue;
            }

            if (char.IsLetterOrDigit(character))
            {
                if (pendingSeparator && builder.Length > 0)
                {
                    builder.Append('-');
                }

                builder.Append(char.ToLowerInvariant(character));
                pendingSeparator = false;
            }
            else
            {
                pendingSeparator = true;
            }
        }

        var normalized = builder.Length == 0 ? "livre-rare" : builder.ToString();
        return Limit(normalized, MaxLength);
    }

    private static string AddSuffix(string value, int collisionSuffix)
    {
        var suffix = $"-{collisionSuffix}";
        var prefix = Limit(value, MaxLength - suffix.Length).TrimEnd('-');
        return prefix + suffix;
    }

    private static string Limit(string value, int maxLength)
    {
        if (value.Length <= maxLength)
        {
            return value;
        }

        return value[..maxLength].TrimEnd('-');
    }
}
