using Vole_Papillon_Damour.Domain.UserAggregate.ValueObjects;

namespace Vole_Papillon_Damour.Domain.UserAggregate;

public sealed class MemberGenreFavorite
{
    public UserId UserId { get; private set; } = null!;
    public string Genre { get; private set; } = null!;

    private MemberGenreFavorite(UserId userId, string genre)
    {
        UserId = userId ?? throw new ArgumentNullException(nameof(userId));
        Genre = Normalize(genre);
    }

    public MemberGenreFavorite()
    {
    }

    public static MemberGenreFavorite Create(UserId userId, string genre) => new(userId, genre);

    private static string Normalize(string genre)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(genre);
        var normalized = string.Join(' ', genre.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
        if (normalized.Length > 100)
        {
            throw new ArgumentOutOfRangeException(nameof(genre), "A genre cannot exceed 100 characters.");
        }

        return normalized;
    }
}
