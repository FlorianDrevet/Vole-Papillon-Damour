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
    public string? Title { get; private set; }
    public string? Authors { get; private set; }
    public string? Publisher { get; private set; }
    public int? PublicationYear { get; private set; }
    public DateTime AddedAt { get; private set; }

    private WatchlistItem(
        Guid id,
        UserId userId,
        WatchlistItemScope scope,
        string? workId,
        Isbn13? isbn13,
        DateTime addedAt,
        string? title,
        string? authors,
        string? publisher,
        int? publicationYear) : base(id)
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
        Title = NormalizeText(title, 500, nameof(title));
        Authors = NormalizeText(authors, 500, nameof(authors));
        Publisher = NormalizeText(publisher, 200, nameof(publisher));
        PublicationYear = NormalizePublicationYear(publicationYear);
        AddedAt = DomainTime.RequireUtc(addedAt, nameof(addedAt));
    }

    public static WatchlistItem CreateEdition(
        Guid id,
        UserId userId,
        Isbn13 isbn13,
        DateTime addedAt,
        string? title = null,
        string? authors = null,
        string? publisher = null,
        int? publicationYear = null)
    {
        return new WatchlistItem(
            id,
            userId,
            WatchlistItemScope.Edition,
            null,
            isbn13,
            addedAt,
            title,
            authors,
            publisher,
            publicationYear);
    }

    public static WatchlistItem CreateWork(
        Guid id,
        UserId userId,
        string workId,
        DateTime addedAt,
        string? title = null,
        string? authors = null,
        string? publisher = null,
        int? publicationYear = null)
    {
        return new WatchlistItem(
            id,
            userId,
            WatchlistItemScope.Work,
            workId,
            null,
            addedAt,
            title,
            authors,
            publisher,
            publicationYear);
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

    private static string? NormalizeText(string? value, int maxLength, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var normalized = value.Trim();
        if (normalized.Length > maxLength)
        {
            throw new ArgumentException(
                $"The watchlist metadata cannot exceed {maxLength} characters.",
                parameterName);
        }

        return normalized;
    }

    private static int? NormalizePublicationYear(int? value)
    {
        if (value is < 1 or > 9999)
        {
            throw new ArgumentOutOfRangeException(
                nameof(value),
                "The publication year must be between 1 and 9999.");
        }

        return value;
    }
}
