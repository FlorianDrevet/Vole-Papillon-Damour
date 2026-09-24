using System.Buffers.Binary;
using System.Buffers.Text;
using System.Security.Cryptography;
using Microsoft.Extensions.Options;
using Vole_Papillon_Damour.Application.Common.Interfaces.Services;

namespace Vole_Papillon_Damour.Infrastructure.Services.MemberCards;

public sealed class MemberCardTokenService : IMemberCardTokenService
{
    private const string Prefix = "VPDC1";
    private readonly byte[] _key;

    public MemberCardTokenService(IOptions<MemberCardTokenOptions> options)
    {
        if (!MemberCardTokenOptions.TryDecodeSigningKey(options.Value.SigningKey, out _key))
        {
            throw new InvalidOperationException("MemberCards:SigningKey must be a Base64 key of at least 32 bytes.");
        }
    }

    public string Create(Guid cardId, int version)
    {
        if (cardId == Guid.Empty)
        {
            throw new ArgumentException("A card identifier is required.", nameof(cardId));
        }

        if (version < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(version), "A card version must be positive.");
        }

        var payload = new byte[20];
        cardId.TryWriteBytes(payload.AsSpan(0, 16));
        BinaryPrimitives.WriteInt32BigEndian(payload.AsSpan(16, 4), version);
        return string.Join('.', Prefix, Base64Url.EncodeToString(payload), Base64Url.EncodeToString(Sign(payload)));
    }

    public bool TryRead(string? token, out Guid cardId, out int version)
    {
        cardId = Guid.Empty;
        version = 0;
        var parts = token?.Split('.');
        if (parts is not { Length: 3 } || parts[0] != Prefix)
        {
            return false;
        }

        byte[] payload;
        byte[] signature;
        try
        {
            payload = Base64Url.DecodeFromChars(parts[1]);
            signature = Base64Url.DecodeFromChars(parts[2]);
        }
        catch (FormatException)
        {
            return false;
        }

        if (payload.Length != 20 || signature.Length != 32 ||
            !CryptographicOperations.FixedTimeEquals(signature, Sign(payload)))
        {
            return false;
        }

        var parsedVersion = BinaryPrimitives.ReadInt32BigEndian(payload.AsSpan(16, 4));
        var parsedCardId = new Guid(payload.AsSpan(0, 16));
        if (parsedVersion < 1 || parsedCardId == Guid.Empty)
        {
            return false;
        }

        cardId = parsedCardId;
        version = parsedVersion;
        return true;
    }

    private byte[] Sign(byte[] payload) => HMACSHA256.HashData(_key, payload);
}
