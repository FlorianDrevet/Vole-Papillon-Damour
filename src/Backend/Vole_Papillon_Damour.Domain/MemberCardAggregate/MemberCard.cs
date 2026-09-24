using Vole_Papillon_Damour.Domain.Common;
using Vole_Papillon_Damour.Domain.Common.Models;
using Vole_Papillon_Damour.Domain.UserAggregate.ValueObjects;

namespace Vole_Papillon_Damour.Domain.MemberCardAggregate;

public sealed class MemberCard : Entity<Guid>
{
    public UserId UserId { get; private set; } = null!;
    public int Version { get; private set; }
    public string RecoveryCode { get; private set; } = string.Empty;
    public DateTime IssuedAt { get; private set; }
    public DateTime? RotatedAt { get; private set; }
    public DateTime? RevokedAt { get; private set; }
    public bool IsActive => RevokedAt is null;

    private MemberCard(Guid id, UserId userId, string recoveryCode, DateTime issuedAt) : base(id)
    {
        if (id == Guid.Empty)
        {
            throw new ArgumentException("A member card identifier is required.", nameof(id));
        }

        if (userId is null || userId.Value == Guid.Empty)
        {
            throw new ArgumentException("A valid member identifier is required.", nameof(userId));
        }

        UserId = userId;
        Version = 1;
        RecoveryCode = RequireNormalisedRecoveryCode(recoveryCode);
        IssuedAt = DomainTime.RequireUtc(issuedAt, nameof(issuedAt));
    }

    private MemberCard()
    {
    }

    public static MemberCard Issue(Guid id, UserId userId, string recoveryCode, DateTime issuedAt) =>
        new(id, userId, recoveryCode, issuedAt);

    public void Rotate(string newRecoveryCode, DateTime rotatedAt)
    {
        if (!IsActive)
        {
            throw new InvalidOperationException("A revoked member card cannot be rotated.");
        }

        var utcRotatedAt = DomainTime.RequireUtc(rotatedAt, nameof(rotatedAt));
        if (utcRotatedAt < IssuedAt || (RotatedAt is not null && utcRotatedAt < RotatedAt))
        {
            throw new ArgumentOutOfRangeException(nameof(rotatedAt), "A card rotation cannot predate its issue or previous rotation.");
        }

        RecoveryCode = RequireNormalisedRecoveryCode(newRecoveryCode);
        Version = checked(Version + 1);
        RotatedAt = utcRotatedAt;
    }

    public void Revoke(DateTime revokedAt)
    {
        var utcRevokedAt = DomainTime.RequireUtc(revokedAt, nameof(revokedAt));
        if (utcRevokedAt < IssuedAt || (RotatedAt is not null && utcRevokedAt < RotatedAt))
        {
            throw new ArgumentOutOfRangeException(nameof(revokedAt), "A card revocation cannot predate its issue or latest rotation.");
        }

        RevokedAt ??= utcRevokedAt;
    }

    private static string RequireNormalisedRecoveryCode(string recoveryCode)
    {
        if (!MemberCardRecoveryCode.TryNormalize(recoveryCode, out var normalized) ||
            !string.Equals(recoveryCode, normalized, StringComparison.Ordinal))
        {
            throw new ArgumentException("A normalised recovery code is required.", nameof(recoveryCode));
        }

        return normalized;
    }
}
