using System.Globalization;

namespace Vole_Papillon_Damour.Domain.MemberCardAggregate;

public static class MemberCardRecoveryCode
{
    public const int DigitCount = 4;

    private static readonly string[] WordList =
    [
        "LUNE", "PLUME", "ROSE", "SABLE", "ARBRE", "AROME", "AUBE", "AVION",
        "BANANE", "BATEAU", "BOCAL", "BLANC", "BRUME", "CADEAU", "CAHIER", "CAJOU",
        "CANARD", "CANAPE", "CARAFE", "CARTE", "CEDRE", "CERISE", "CHANT", "CHOUX",
        "CLOCHE", "COEUR", "DAME", "DANSE", "DUNE", "ECLAT", "ECUME", "EPICE",
        "ETOILE", "FABLE", "FLEUR", "FLUTE", "GOUTTE", "GRAIN", "GRACE", "HARPE",
        "HERBE", "JAUNE", "JASMIN", "JOLIE", "JOUET", "LAMPE", "LARME", "LAVABO",
        "LEGUME", "LILAS", "LIVRE", "MAMAN", "MARIN", "MELON", "MENTHE", "MONTRE",
        "NUAGE", "OLIVE", "ORAGE", "PAPAYE", "PAVOT", "PLAGE", "POIRE", "POMME"
    ];

    public static IReadOnlyList<string> Words { get; } = Array.AsReadOnly(WordList);

    public static string Generate(Func<int, int> nextInt)
    {
        ArgumentNullException.ThrowIfNull(nextInt);

        var wordIndex = nextInt(Words.Count);
        var number = nextInt(10_000);
        if (wordIndex < 0 || wordIndex >= Words.Count || number < 0 || number >= 10_000)
        {
            throw new ArgumentOutOfRangeException(nameof(nextInt), "The index source returned a value outside its requested range.");
        }

        return string.Concat(Words[wordIndex], "-", number.ToString("D4", CultureInfo.InvariantCulture));
    }

    public static bool TryNormalize(string? input, out string normalized)
    {
        normalized = string.Empty;
        if (string.IsNullOrWhiteSpace(input))
        {
            return false;
        }

        var compact = new string(input.Where(character => !char.IsWhiteSpace(character) && character != '-')
            .ToArray()).ToUpperInvariant();
        if (compact.Length <= DigitCount)
        {
            return false;
        }

        var word = compact[..^DigitCount];
        var digits = compact[^DigitCount..];
        if (!digits.All(char.IsAsciiDigit) || !Words.Contains(word, StringComparer.Ordinal))
        {
            return false;
        }

        normalized = string.Concat(word, "-", digits);
        return true;
    }
}
