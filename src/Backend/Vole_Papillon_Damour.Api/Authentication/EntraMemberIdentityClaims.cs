using System.Security.Claims;

namespace Vole_Papillon_Damour.Api.Authentication;

public sealed record EntraMemberIdentityClaims(
    Guid ExternalId,
    string Email,
    string? FirstName,
    string? LastName);

public static class EntraMemberIdentityClaimsReader
{
    private static readonly string[] EmailClaimTypes =
    [
        ClaimTypes.Email,
        "email",
        "emails",
        "preferred_username",
        ClaimTypes.Upn,
        "http://schemas.xmlsoap.org/ws/2005/05/identity/claims/emailaddress",
    ];

    private static readonly string[] FirstNameClaimTypes =
    [
        "given_name",
        "givenName",
        ClaimTypes.GivenName,
    ];

    private static readonly string[] LastNameClaimTypes =
    [
        "family_name",
        "surname",
        ClaimTypes.Surname,
    ];

    private static readonly string[] DisplayNameClaimTypes =
    [
        "name",
        ClaimTypes.Name,
        "displayName",
    ];

    public static bool TryRead(
        ClaimsPrincipal principal,
        out EntraMemberIdentityClaims? identity)
    {
        ArgumentNullException.ThrowIfNull(principal);

        var externalIdValue = principal.FindFirst("oid")?.Value
            ?? principal.FindFirst(
                "http://schemas.microsoft.com/identity/claims/objectidentifier")?.Value;
        var email = ReadClaim(principal, EmailClaimTypes);

        if (!Guid.TryParse(externalIdValue, out var externalId) ||
            externalId == Guid.Empty ||
            email is null ||
            email.Length > 320)
        {
            identity = null;
            return false;
        }

        var firstName = ReadClaim(principal, FirstNameClaimTypes);
        var lastName = ReadClaim(principal, LastNameClaimTypes);
        if (firstName is null && lastName is null)
        {
            (firstName, lastName) = SplitDisplayName(ReadClaim(principal, DisplayNameClaimTypes));
        }

        identity = new EntraMemberIdentityClaims(externalId, email, firstName, lastName);
        return true;
    }

    private static string? ReadClaim(
        ClaimsPrincipal principal,
        IEnumerable<string> claimTypes)
    {
        return claimTypes
            .Select(principal.FindFirst)
            .Select(claim => Normalize(claim?.Value))
            .FirstOrDefault(value => value is not null);
    }

    private static (string? FirstName, string? LastName) SplitDisplayName(string? displayName)
    {
        if (displayName is null)
        {
            return (null, null);
        }

        var parts = displayName.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
        return parts.Length switch
        {
            0 => (null, null),
            1 => (parts[0], null),
            _ => (parts[0], string.Join(' ', parts.Skip(1))),
        };
    }

    private static string? Normalize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var normalized = value.Trim();
        return normalized.Equals("unknown", StringComparison.OrdinalIgnoreCase) ||
               normalized.Equals("undefined", StringComparison.OrdinalIgnoreCase) ||
               normalized.Equals("null", StringComparison.OrdinalIgnoreCase)
            ? null
            : normalized;
    }
}
