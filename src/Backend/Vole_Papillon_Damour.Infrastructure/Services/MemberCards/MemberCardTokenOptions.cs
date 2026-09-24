namespace Vole_Papillon_Damour.Infrastructure.Services.MemberCards;

public sealed class MemberCardTokenOptions
{
    public const string SectionName = "MemberCards";

    public string SigningKey { get; init; } = string.Empty;

    public static bool TryDecodeSigningKey(string? signingKey, out byte[] key)
    {
        key = [];
        if (string.IsNullOrWhiteSpace(signingKey))
        {
            return false;
        }

        try
        {
            key = Convert.FromBase64String(signingKey);
            return key.Length >= 32;
        }
        catch (FormatException)
        {
            return false;
        }
    }
}
