using System.Text;
using Vole_Papillon_Damour.Domain.Common;
using Vole_Papillon_Damour.Domain.Common.Models;
using Vole_Papillon_Damour.Domain.UserAggregate.ValueObjects;

namespace Vole_Papillon_Damour.Domain.ManualRequestAggregate;

/// <summary>
/// A person without an account whose wanted books are entered by an administrator
/// (spec manual-book-requests, RG-78, RG-87). Contact details are erased in place so
/// closed requests keep their history without personal data (RG-88, RG-89).
/// </summary>
public sealed class ManualRequester : AggregateRoot<Guid>
{
    public const int NameMaxLength = 100;
    public const int PhoneMaxLength = 16;

    public string? LastName { get; private set; }
    public string? FirstName { get; private set; }
    public string? Phone { get; private set; }
    public DateTime ConsentRecordedAt { get; private set; }
    public UserId ConsentRecordedBy { get; private set; } = null!;
    public DateTime CreatedAt { get; private set; }
    public DateTime UpdatedAt { get; private set; }
    public DateTime? ErasedAt { get; private set; }

    private ManualRequester(
        Guid id,
        string lastName,
        string firstName,
        string phone,
        UserId recordedBy,
        DateTime at) : base(id)
    {
        if (id == Guid.Empty)
        {
            throw new ArgumentException("A requester identifier is required.", nameof(id));
        }

        ValidateAuthor(recordedBy);
        var utcAt = DomainTime.RequireUtc(at, nameof(at));
        LastName = NormalizeName(lastName, nameof(lastName));
        FirstName = NormalizeName(firstName, nameof(firstName));
        Phone = NormalizePhone(phone);
        ConsentRecordedAt = utcAt;
        ConsentRecordedBy = recordedBy;
        CreatedAt = utcAt;
        UpdatedAt = utcAt;
    }

    public ManualRequester() : base()
    {
    }

    public bool IsErased => ErasedAt is not null;

    public static ManualRequester Create(
        Guid id,
        string lastName,
        string firstName,
        string phone,
        bool consentGiven,
        UserId recordedBy,
        DateTime at)
    {
        if (!consentGiven)
        {
            throw new ArgumentException("The requester's consent is required (RG-87).", nameof(consentGiven));
        }

        return new ManualRequester(id, lastName, firstName, phone, recordedBy, at);
    }

    public void UpdateContact(string lastName, string firstName, string phone, DateTime at)
    {
        EnsureNotErased();
        var utcAt = DomainTime.RequireUtc(at, nameof(at));
        LastName = NormalizeName(lastName, nameof(lastName));
        FirstName = NormalizeName(firstName, nameof(firstName));
        Phone = NormalizePhone(phone);
        UpdatedAt = utcAt;
    }

    public void Erase(DateTime at)
    {
        if (IsErased)
        {
            return;
        }

        var utcAt = DomainTime.RequireUtc(at, nameof(at));
        LastName = null;
        FirstName = null;
        Phone = null;
        ErasedAt = utcAt;
        UpdatedAt = utcAt;
    }

    /// <summary>
    /// Keeps a leading "+" and the digits; spaces, dots, dashes and parentheses are
    /// dropped. A French number has 10 digits starting with 0; an international one
    /// has 8 to 15 digits after "+".
    /// </summary>
    public static string NormalizePhone(string phone)
    {
        if (string.IsNullOrWhiteSpace(phone))
        {
            throw new ArgumentException("A phone number is required.", nameof(phone));
        }

        var builder = new StringBuilder(phone.Length);
        var trimmed = phone.Trim();
        for (var index = 0; index < trimmed.Length; index++)
        {
            var character = trimmed[index];
            if (char.IsAsciiDigit(character))
            {
                builder.Append(character);
            }
            else if (character == '+' && index == 0)
            {
                builder.Append(character);
            }
            else if (character is not (' ' or '.' or '-' or '(' or ')'))
            {
                throw new ArgumentException("The phone number contains invalid characters.", nameof(phone));
            }
        }

        var normalized = builder.ToString();
        var isFrench = normalized.Length == 10 && normalized[0] == '0';
        var isInternational = normalized.StartsWith('+') && normalized.Length is >= 9 and <= PhoneMaxLength;
        if (!isFrench && !isInternational)
        {
            throw new ArgumentException("The phone number is not valid.", nameof(phone));
        }

        return normalized;
    }

    private void EnsureNotErased()
    {
        if (IsErased)
        {
            throw new InvalidOperationException("An erased requester cannot be changed.");
        }
    }

    private static string NormalizeName(string value, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("A name is required.", parameterName);
        }

        var normalized = value.Trim();
        if (normalized.Length > NameMaxLength)
        {
            throw new ArgumentException($"A name cannot exceed {NameMaxLength} characters.", parameterName);
        }

        return normalized;
    }

    private static void ValidateAuthor(UserId? author)
    {
        if (author is null || author.Value == Guid.Empty)
        {
            throw new ArgumentException("A valid administrator identifier is required.", nameof(author));
        }
    }
}
