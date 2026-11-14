using Vole_Papillon_Damour.Domain.BookAggregate.ValueObjects;
using Vole_Papillon_Damour.Domain.Common;
using Vole_Papillon_Damour.Domain.UserAggregate.ValueObjects;

namespace Vole_Papillon_Damour.Domain.FavoriteBookAggregate;

public sealed class MemberFavoriteBook
{
    public UserId UserId { get; private set; } = null!;
    public Isbn13 Isbn13 { get; private set; }
    public DateTime AddedAt { get; private set; }

    private MemberFavoriteBook()
    {
    }

    public static MemberFavoriteBook Create(UserId userId, Isbn13 isbn13, DateTime addedAt)
    {
        ArgumentNullException.ThrowIfNull(userId);
        if (userId.Value == Guid.Empty)
        {
            throw new ArgumentException("A valid user identifier is required.", nameof(userId));
        }

        if (string.IsNullOrWhiteSpace(isbn13.Value))
        {
            throw new ArgumentException("A valid ISBN-13 is required.", nameof(isbn13));
        }

        return new MemberFavoriteBook
        {
            UserId = userId,
            Isbn13 = isbn13,
            AddedAt = DomainTime.RequireUtc(addedAt, nameof(addedAt)),
        };
    }
}
