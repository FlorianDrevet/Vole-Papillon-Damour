using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;
using Vole_Papillon_Damour.Application.Common.Interfaces.Services;

namespace Vole_Papillon_Damour.Infrastructure.Services.BookAlerts;

/// <summary>
/// Mints "payload.signature" tokens where the payload carries the member and an
/// expiry, and the signature is an HMAC-SHA256 over that payload. The token is
/// opaque to the mailbox provider and cannot be forged without the signing key,
/// which is what lets the one-click endpoint stay anonymous.
/// </summary>
public sealed class UnsubscribeTokenService(
    IOptions<UnsubscribeTokenOptions> options,
    IDateTimeProvider dateTimeProvider)
    : IUnsubscribeTokenService
{
    private readonly UnsubscribeTokenOptions _options = options.Value;

    public string Create(Guid memberId)
    {
        var expiresAt = dateTimeProvider.UtcNow.AddDays(_options.LifetimeDays);
        var payload = FormattableString.Invariant(
            $"{memberId:N}:{new DateTimeOffset(expiresAt, TimeSpan.Zero).ToUnixTimeSeconds()}");
        var payloadBytes = Encoding.UTF8.GetBytes(payload);

        return string.Concat(
            Base64Url.Encode(payloadBytes),
            ".",
            Base64Url.Encode(Sign(payloadBytes)));
    }

    public bool TryValidate(string token, out Guid memberId)
    {
        memberId = Guid.Empty;
        if (string.IsNullOrWhiteSpace(token))
        {
            return false;
        }

        var separator = token.IndexOf('.', StringComparison.Ordinal);
        if (separator <= 0 || separator == token.Length - 1)
        {
            return false;
        }

        if (!Base64Url.TryDecode(token[..separator], out var payloadBytes) ||
            !Base64Url.TryDecode(token[(separator + 1)..], out var signatureBytes))
        {
            return false;
        }

        if (!CryptographicOperations.FixedTimeEquals(Sign(payloadBytes), signatureBytes))
        {
            return false;
        }

        var parts = Encoding.UTF8.GetString(payloadBytes).Split(':');
        if (parts.Length != 2 ||
            !Guid.TryParseExact(parts[0], "N", out var parsedMemberId) ||
            !long.TryParse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture, out var expiresAt))
        {
            return false;
        }

        if (DateTimeOffset.FromUnixTimeSeconds(expiresAt) <= new DateTimeOffset(
                dateTimeProvider.UtcNow,
                TimeSpan.Zero))
        {
            return false;
        }

        memberId = parsedMemberId;
        return true;
    }

    private byte[] Sign(byte[] payloadBytes)
    {
        if (string.IsNullOrWhiteSpace(_options.SigningKey))
        {
            throw new InvalidOperationException(
                "The unsubscribe token signing key is not configured.");
        }

        return HMACSHA256.HashData(DecodeKey(_options.SigningKey), payloadBytes);
    }

    // The key is a Key Vault secret, so it arrives base64-encoded in practice;
    // a raw passphrase is accepted too rather than failing at runtime.
    private static byte[] DecodeKey(string signingKey)
    {
        Span<byte> decoded = stackalloc byte[signingKey.Length];
        return Convert.TryFromBase64String(signingKey, decoded, out var written)
            ? decoded[..written].ToArray()
            : Encoding.UTF8.GetBytes(signingKey);
    }

    private static class Base64Url
    {
        public static string Encode(byte[] value)
        {
            return Convert.ToBase64String(value)
                .TrimEnd('=')
                .Replace('+', '-')
                .Replace('/', '_');
        }

        public static bool TryDecode(string value, out byte[] decoded)
        {
            decoded = [];
            var padded = value.Replace('-', '+').Replace('_', '/');
            padded += (padded.Length % 4) switch
            {
                2 => "==",
                3 => "=",
                0 => string.Empty,
                _ => "\0"
            };
            if (padded.EndsWith('\0'))
            {
                return false;
            }

            var buffer = new byte[padded.Length / 4 * 3];
            if (!Convert.TryFromBase64String(padded, buffer, out var written))
            {
                return false;
            }

            decoded = buffer[..written];
            return true;
        }
    }
}
