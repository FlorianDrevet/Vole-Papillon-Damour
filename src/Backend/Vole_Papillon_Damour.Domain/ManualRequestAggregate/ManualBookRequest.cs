using Vole_Papillon_Damour.Domain.BookAggregate.ValueObjects;
using Vole_Papillon_Damour.Domain.BookMovementAggregate.ValueObjects;
using Vole_Papillon_Damour.Domain.Common;
using Vole_Papillon_Damour.Domain.Common.Models;
using Vole_Papillon_Damour.Domain.EventsAggregate.ValueObjects;
using Vole_Papillon_Damour.Domain.ManualRequestAggregate.ValueObjects;
using Vole_Papillon_Damour.Domain.ScanSessionAggregate.ValueObjects;
using Vole_Papillon_Damour.Domain.UserAggregate.ValueObjects;

namespace Vole_Papillon_Damour.Domain.ManualRequestAggregate;

/// <summary>
/// One wanted book of a <see cref="ManualRequester"/>. Life cycle: Searching → Found →
/// Given | Refused, or back to Searching; Searching/Found → Cancelled (spec §5).
/// <see cref="Version"/> is an optimistic concurrency token: two scan stations cannot
/// claim the same request (spec §10).
/// </summary>
public sealed class ManualBookRequest : Entity<Guid>
{
    public const int TextMaxLength = 500;
    public const int WorkIdMaxLength = 64;

    public Guid RequesterId { get; private set; }
    public ManualBookRequestScope Scope { get; private set; }
    public string? WorkId { get; private set; }
    public Isbn13? Isbn13 { get; private set; }
    public string? Title { get; private set; }
    public string? Authors { get; private set; }
    public string? Publisher { get; private set; }
    public int? PublicationYear { get; private set; }
    public DateTime RequestedAt { get; private set; }
    public ManualBookRequestStatus Status { get; private set; } = ManualBookRequestStatus.Searching;

    public ManualBookRequestFoundOrigin? FoundOrigin { get; private set; }
    public Isbn13? FoundIsbn13 { get; private set; }
    public DateTime? FoundAt { get; private set; }
    public UserId? FoundBy { get; private set; }
    /// <summary>Scan session of the hold, or of the announcement a copy was picked from.</summary>
    public ScanSessionId? FoundScanSessionId { get; private set; }
    public AssoEventsId? PickedFromFairId { get; private set; }
    public BookMovementId? HoldMovementId { get; private set; }

    public DateTime? ClosedAt { get; private set; }
    public UserId? ClosedBy { get; private set; }
    public BookMovementId? ReleaseMovementId { get; private set; }

    public int Version { get; private set; }
    public DateTime UpdatedAt { get; private set; }

    private ManualBookRequest(
        Guid id,
        Guid requesterId,
        ManualBookRequestScope scope,
        string? workId,
        Isbn13? isbn13,
        string? title,
        string? authors,
        string? publisher,
        int? publicationYear,
        DateTime requestedAt) : base(id)
    {
        if (id == Guid.Empty)
        {
            throw new ArgumentException("A request identifier is required.", nameof(id));
        }

        if (requesterId == Guid.Empty)
        {
            throw new ArgumentException("A requester identifier is required.", nameof(requesterId));
        }

        var normalizedWorkId = string.IsNullOrWhiteSpace(workId) ? null : workId.Trim();
        if (scope == ManualBookRequestScope.Work)
        {
            if (normalizedWorkId is null || normalizedWorkId.Length > WorkIdMaxLength || isbn13 is not null)
            {
                throw new ArgumentException("A work request requires a work identifier only.", nameof(workId));
            }
        }
        else if (scope == ManualBookRequestScope.Edition)
        {
            if (isbn13 is null || normalizedWorkId is not null)
            {
                throw new ArgumentException("An edition request requires an ISBN-13 only.", nameof(isbn13));
            }
        }
        else
        {
            throw new ArgumentOutOfRangeException(nameof(scope));
        }

        RequesterId = requesterId;
        Scope = scope;
        WorkId = normalizedWorkId;
        Isbn13 = isbn13;
        Title = NormalizeText(title, nameof(title));
        Authors = NormalizeText(authors, nameof(authors));
        Publisher = NormalizeText(publisher, nameof(publisher));
        PublicationYear = publicationYear;
        RequestedAt = DomainTime.RequireUtc(requestedAt, nameof(requestedAt));
        UpdatedAt = RequestedAt;
    }

    public ManualBookRequest() : base()
    {
    }

    public bool IsOpen => Status is ManualBookRequestStatus.Searching or ManualBookRequestStatus.Found;

    public static ManualBookRequest CreateWork(
        Guid id,
        Guid requesterId,
        string workId,
        string? title,
        string? authors,
        string? publisher,
        int? publicationYear,
        DateTime requestedAt)
        => new(id, requesterId, ManualBookRequestScope.Work, workId, null, title, authors, publisher, publicationYear, requestedAt);

    public static ManualBookRequest CreateEdition(
        Guid id,
        Guid requesterId,
        Isbn13 isbn13,
        string? title,
        string? authors,
        string? publisher,
        int? publicationYear,
        DateTime requestedAt)
        => new(id, requesterId, ManualBookRequestScope.Edition, null, isbn13, title, authors, publisher, publicationYear, requestedAt);

    /// <summary>RG-79: this ISBN, or an edition of this work.</summary>
    public bool Matches(Isbn13 scannedIsbn13, string? scannedWorkId)
    {
        return Scope == ManualBookRequestScope.Edition
            ? Isbn13 == scannedIsbn13
            : scannedWorkId is not null && string.Equals(WorkId, scannedWorkId, StringComparison.Ordinal);
    }

    public void MarkFoundByScan(
        Isbn13 foundIsbn13,
        ScanSessionId scanSessionId,
        BookMovementId holdMovementId,
        UserId volunteerId,
        DateTime at)
    {
        ArgumentNullException.ThrowIfNull(scanSessionId);
        ArgumentNullException.ThrowIfNull(holdMovementId);
        MarkFound(foundIsbn13, ManualBookRequestFoundOrigin.Scan, scanSessionId, null, holdMovementId, volunteerId, at);
    }

    /// <summary>
    /// RG-82. A copy taken from an announcement remembers its fair and the scan session
    /// of that announcement, so a refusal can announce it again on the same fair.
    /// </summary>
    public void MarkFoundFromStock(
        Isbn13 foundIsbn13,
        AssoEventsId? pickedFromFairId,
        ScanSessionId? pickedAnnouncementSessionId,
        BookMovementId pickMovementId,
        UserId administratorId,
        DateTime at)
    {
        ArgumentNullException.ThrowIfNull(pickMovementId);
        if ((pickedFromFairId is null) != (pickedAnnouncementSessionId is null))
        {
            throw new ArgumentException("An announced copy needs both its fair and its announcement session.", nameof(pickedAnnouncementSessionId));
        }

        var origin = pickedFromFairId is null
            ? ManualBookRequestFoundOrigin.StockAvailable
            : ManualBookRequestFoundOrigin.StockAnnounced;
        MarkFound(foundIsbn13, origin, pickedAnnouncementSessionId, pickedFromFairId, pickMovementId, administratorId, at);
    }

    /// <summary>RG-83.</summary>
    public void MarkGiven(UserId administratorId, DateTime at)
    {
        EnsureStatus(ManualBookRequestStatus.Found);
        Close(ManualBookRequestStatus.Given, administratorId, releaseMovementId: null, at);
    }

    /// <summary>RG-84. The found copy has been released into stock by <paramref name="releaseMovementId"/>.</summary>
    public void Refuse(UserId administratorId, bool keepSearching, BookMovementId releaseMovementId, DateTime at)
    {
        EnsureStatus(ManualBookRequestStatus.Found);
        ArgumentNullException.ThrowIfNull(releaseMovementId);
        if (keepSearching)
        {
            ValidateAuthor(administratorId);
            ClearFound();
            Status = ManualBookRequestStatus.Searching;
            Touch(at);
            return;
        }

        Close(ManualBookRequestStatus.Refused, administratorId, releaseMovementId, at);
    }

    /// <summary>RG-86. A found request needs the movement that released its copy.</summary>
    public void Cancel(UserId administratorId, BookMovementId? releaseMovementId, DateTime at)
    {
        if (!IsOpen)
        {
            throw new InvalidOperationException("Only an open request can be cancelled.");
        }

        if (Status == ManualBookRequestStatus.Found && releaseMovementId is null)
        {
            throw new ArgumentNullException(nameof(releaseMovementId), "A found request releases its copy when cancelled.");
        }

        Close(ManualBookRequestStatus.Cancelled, administratorId, releaseMovementId, at);
    }

    /// <summary>RG-85: the scan that held the copy was removed or its session cancelled.</summary>
    public void RevertScanHold(DateTime at)
    {
        EnsureStatus(ManualBookRequestStatus.Found);
        if (FoundOrigin != ManualBookRequestFoundOrigin.Scan)
        {
            throw new InvalidOperationException("Only a copy held at scan can be reverted by a session correction.");
        }

        ClearFound();
        Status = ManualBookRequestStatus.Searching;
        Touch(at);
    }

    private void MarkFound(
        Isbn13 foundIsbn13,
        ManualBookRequestFoundOrigin origin,
        ScanSessionId? scanSessionId,
        AssoEventsId? pickedFromFairId,
        BookMovementId movementId,
        UserId author,
        DateTime at)
    {
        EnsureStatus(ManualBookRequestStatus.Searching);
        ValidateAuthor(author);
        if (string.IsNullOrWhiteSpace(foundIsbn13.Value))
        {
            throw new ArgumentException("A valid ISBN-13 is required.", nameof(foundIsbn13));
        }

        var utcAt = DomainTime.RequireUtc(at, nameof(at));
        Status = ManualBookRequestStatus.Found;
        FoundOrigin = origin;
        FoundIsbn13 = foundIsbn13;
        FoundAt = utcAt;
        FoundBy = author;
        FoundScanSessionId = scanSessionId;
        PickedFromFairId = pickedFromFairId;
        HoldMovementId = movementId;
        Touch(utcAt);
    }

    private void Close(ManualBookRequestStatus status, UserId author, BookMovementId? releaseMovementId, DateTime at)
    {
        ValidateAuthor(author);
        var utcAt = DomainTime.RequireUtc(at, nameof(at));
        Status = status;
        ClosedAt = utcAt;
        ClosedBy = author;
        ReleaseMovementId = releaseMovementId;
        Touch(utcAt);
    }

    private void ClearFound()
    {
        FoundOrigin = null;
        FoundIsbn13 = null;
        FoundAt = null;
        FoundBy = null;
        FoundScanSessionId = null;
        PickedFromFairId = null;
        HoldMovementId = null;
    }

    private void Touch(DateTime at)
    {
        UpdatedAt = DomainTime.RequireUtc(at, nameof(at));
        Version++;
    }

    private void EnsureStatus(ManualBookRequestStatus expected)
    {
        if (Status != expected)
        {
            throw new InvalidOperationException($"The request must be {expected} but is {Status}.");
        }
    }

    private static void ValidateAuthor(UserId? author)
    {
        if (author is null || author.Value == Guid.Empty)
        {
            throw new ArgumentException("A valid author identifier is required.", nameof(author));
        }
    }

    private static string? NormalizeText(string? value, string parameterName)
    {
        var normalized = string.IsNullOrWhiteSpace(value) ? null : value.Trim();
        if (normalized is { Length: > TextMaxLength })
        {
            throw new ArgumentException($"The value cannot exceed {TextMaxLength} characters.", parameterName);
        }

        return normalized;
    }
}
