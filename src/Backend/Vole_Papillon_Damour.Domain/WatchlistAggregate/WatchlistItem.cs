using Vole_Papillon_Damour.Domain.BookAggregate.ValueObjects;
using Vole_Papillon_Damour.Domain.Common;
using Vole_Papillon_Damour.Domain.Common.Models;
using Vole_Papillon_Damour.Domain.UserAggregate.ValueObjects;
using Vole_Papillon_Damour.Domain.WatchlistAggregate.ValueObjects;

namespace Vole_Papillon_Damour.Domain.WatchlistAggregate;

public sealed class WatchlistItem : Entity<Guid>
{
    public UserId UserId { get; private set; } = null!;
    public WatchlistItemScope Scope { get; private set; }
    public string? WorkId { get; private set; }
    public Isbn13? Isbn13 { get; private set; }
    public DateTime AddedAt { get; private set; }

    private WatchlistItem(
        Guid id,
        UserId userId,
        WatchlistItemScope scope,
        string? workId,
        Isbn13? isbn13,
        DateTime addedAt) : base(id)
    {
        if (id == Guid.Empty)
        {
            throw new ArgumentException("A watchlist item identifier is required.", nameof(id));
        }

        if (userId is null || userId.Value == Guid.Empty)
        {
            throw new ArgumentException("A valid user identifier is required.", nameof(userId));
        }

        if (scope == WatchlistItemScope.Work)
        {
            if (string.IsNullOrWhiteSpace(workId) || isbn13 is not null)
            {
                throw new ArgumentException(
                    "A work watchlist item requires a work identifier only.",
                    nameof(workId));
            }
        }
        else if (scope == WatchlistItemScope.Edition)
        {
            if (isbn13 is null || workId is not null)
            {
                throw new ArgumentException(
                    "An edition watchlist item requires an ISBN only.",
                    nameof(isbn13));
            }
        }
        else
        {
            throw new ArgumentOutOfRangeException(nameof(scope));
        }

        UserId = userId;
        Scope = scope;
        WorkId = string.IsNullOrWhiteSpace(workId) ? null : workId.Trim();
        Isbn13 = isbn13;
        AddedAt = DomainTime.RequireUtc(addedAt, nameof(addedAt));
    }

    public static WatchlistItem CreateEdition(
        Guid id,
        UserId userId,
        Isbn13 isbn13,
        DateTime addedAt)
    {
        return new WatchlistItem(id, userId, WatchlistItemScope.Edition, null, isbn13, addedAt);
    }

    public static WatchlistItem CreateWork(
        Guid id,
        UserId userId,
        string workId,
        DateTime addedAt)
    {
        return new WatchlistItem(id, userId, WatchlistItemScope.Work, workId, null, addedAt);
    }

    public WatchlistItem()
    {
    }

    public bool RedirectEdition(Isbn13 canonicalIsbn13)
    {
        if (Scope != WatchlistItemScope.Edition)
        {
            return false;
        }

        var changed = Isbn13 != canonicalIsbn13 || WorkId is not null;
        Isbn13 = canonicalIsbn13;
        WorkId = null;
        return changed;
    }
}
