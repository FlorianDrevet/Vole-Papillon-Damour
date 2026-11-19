namespace Vole_Papillon_Damour.Domain.BookAggregate.ValueObjects;

// Identifiant d'un livre de la bourse. Deux formes, toutes deux stockées sur 13 chiffres :
// - un ISBN-13 (préfixe 978 ou 979) : l'identifiant d'une édition ;
// - un code interne (EAN-13 à préfixe 2xx, RG-170) : attribué par la plateforme à un livre
//   sans ISBN. Il se lit comme un ISBN par la scanette.
public readonly record struct BookCode
{
    private const char InternalPrefix = '2';

    private BookCode(string value, bool isInternal)
    {
        Value = value;
        IsInternal = isInternal;
    }

    public string Value { get; }

    public bool IsInternal { get; }

    public bool IsIsbn => !IsInternal;

    public static bool operator <(BookCode left, BookCode right) =>
        string.CompareOrdinal(left.Value, right.Value) < 0;

    public static bool operator >(BookCode left, BookCode right) =>
        string.CompareOrdinal(left.Value, right.Value) > 0;

    public static implicit operator BookCode(Isbn13 isbn13)
    {
        if (string.IsNullOrWhiteSpace(isbn13.Value))
        {
            throw new ArgumentException("A valid book code is required.", nameof(isbn13));
        }

        return new BookCode(isbn13.Value, isInternal: false);
    }

    // Accepte un ISBN-10 ou un ISBN-13 (converti) ou un code interne valide.
    public static bool TryCreate(string? input, out BookCode code)
    {
        code = default;

        if (string.IsNullOrWhiteSpace(input))
        {
            return false;
        }

        if (Isbn13.TryCreate(input, out var isbn13))
        {
            code = isbn13;
            return true;
        }

        var normalized = string.Concat(input.Where(character => !char.IsWhiteSpace(character) && character != '-'));
        if (IsValidInternalCode(normalized))
        {
            code = new BookCode(normalized, isInternal: true);
            return true;
        }

        return false;
    }

    // Construit un code interne déjà attribué (lecture depuis la base ou un message).
    public static BookCode CreateInternal(string value)
    {
        if (!IsValidInternalCode(value))
        {
            throw new ArgumentException($"'{value}' is not a valid internal book code.", nameof(value));
        }

        return new BookCode(value, isInternal: true);
    }

    // Plus grand numéro de séquence représentable : 11 chiffres après le préfixe 2.
    public const long MaximumInternalSequenceNumber = 99_999_999_999L;

    // Construit le code interne du numéro de séquence donné : préfixe 2, numéro sur 11 chiffres,
    // clé de contrôle EAN-13. Un numéro n'est jamais réémis (RG-171) : c'est le rôle du compteur.
    public static BookCode CreateInternalFromSequence(long sequenceNumber)
    {
        if (sequenceNumber is < 1 or > MaximumInternalSequenceNumber)
        {
            throw new ArgumentOutOfRangeException(
                nameof(sequenceNumber),
                sequenceNumber,
                "The internal book code sequence number is out of range.");
        }

        var body = InternalPrefix + sequenceNumber.ToString("D11", System.Globalization.CultureInfo.InvariantCulture);
        return CreateInternal(body + CalculateCheckDigit(body));
    }
    // Le code est-il un code interne (préfixe 2xx, clé de contrôle valide) ?
    public static bool IsValidInternalCode(string? value)
    {
        if (value is null || value.Length != 13 || value[0] != InternalPrefix)
        {
            return false;
        }

        if (value.Any(character => character is < '0' or > '9'))
        {
            return false;
        }

        return value[12] - '0' == CalculateCheckDigit(value[..12]);
    }

    // Retourne l'ISBN-13 correspondant, ou false pour un code interne.
    public bool TryGetIsbn13(out Isbn13 isbn13)
    {
        isbn13 = default;

        if (IsInternal)
        {
            return false;
        }

        return Isbn13.TryCreate(Value, out isbn13);
    }

    // ISBN-13 du code, ou exception pour un code interne (aucune source externe ne le connaît).
    public Isbn13 AsIsbn13()
    {
        if (!TryGetIsbn13(out var isbn13))
        {
            throw new InvalidOperationException($"Internal book code {Value} has no ISBN.");
        }

        return isbn13;
    }

    public override string ToString() => Value;

    private static int CalculateCheckDigit(string firstTwelveDigits)
    {
        var sum = 0;
        for (var index = 0; index < firstTwelveDigits.Length; index++)
        {
            sum += (firstTwelveDigits[index] - '0') * (index % 2 == 0 ? 1 : 3);
        }

        return (10 - sum % 10) % 10;
    }
}
