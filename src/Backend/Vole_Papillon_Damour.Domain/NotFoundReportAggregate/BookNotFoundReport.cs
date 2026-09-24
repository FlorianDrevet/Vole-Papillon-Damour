using Vole_Papillon_Damour.Domain.BookAggregate.ValueObjects;
using Vole_Papillon_Damour.Domain.BookMovementAggregate.ValueObjects;
using Vole_Papillon_Damour.Domain.Common;
using Vole_Papillon_Damour.Domain.Common.Models;
using Vole_Papillon_Damour.Domain.NotFoundReportAggregate.ValueObjects;
using Vole_Papillon_Damour.Domain.RareBookAggregate.ValueObjects;
using Vole_Papillon_Damour.Domain.UserAggregate.ValueObjects;

namespace Vole_Papillon_Damour.Domain.NotFoundReportAggregate;

public sealed class BookNotFoundReport : Entity<Guid>
{
    public const int CommentMaxLength = 280;
    public const int ClosureNoteMaxLength = 500;

    public UserId? UserId { get; private set; }
    public Isbn13? Isbn13 { get; private set; }
    public RareBookId? RareBookId { get; private set; }
    public NotFoundReportLocation? Location { get; private set; }
    public string? Comment { get; private set; }
    public NotFoundReportStatus Status { get; private set; } = NotFoundReportStatus.Open;
    public DateTime ReportedAt { get; private set; }
    public DateTime? ClosedAt { get; private set; }
    public UserId? ClosedBy { get; private set; }
    public string? ClosureNote { get; private set; }
    public NotFoundWithdrawalReason? WithdrawalReason { get; private set; }
    public int? WithdrawnQuantity { get; private set; }
    public BookMovementId? WithdrawalMovementId { get; private set; }

    private BookNotFoundReport(
        Guid id,
        UserId userId,
        Isbn13? isbn13,
        RareBookId? rareBookId,
        NotFoundReportLocation? location,
        string? comment,
        DateTime reportedAt) : base(id)
    {
        if (id == Guid.Empty)
        {
            throw new ArgumentException("A report identifier is required.", nameof(id));
        }

        if (userId is null || userId.Value == Guid.Empty)
        {
            throw new ArgumentException("A valid member identifier is required.", nameof(userId));
        }

        if ((isbn13 is null) == (rareBookId is null))
        {
            throw new ArgumentException("A report targets exactly one edition or one rare book.");
        }

        if (isbn13 is { } edition && string.IsNullOrWhiteSpace(edition.Value))
        {
            throw new ArgumentException("A valid ISBN-13 is required.", nameof(isbn13));
        }

        if (rareBookId is { Value: var rareId } && rareId == Guid.Empty)
        {
            throw new ArgumentException("A valid rare book identifier is required.", nameof(rareBookId));
        }

        if (location is { } reportLocation && !Enum.IsDefined(reportLocation))
        {
            throw new ArgumentOutOfRangeException(nameof(location), reportLocation, "Unknown report location.");
        }

        UserId = userId;
        Isbn13 = isbn13;
        RareBookId = rareBookId;
        Location = location;
        Comment = NormalizeComment(comment);
        ReportedAt = DomainTime.RequireUtc(reportedAt, nameof(reportedAt));
    }

    public BookNotFoundReport() : base()
    {
    }

    public bool IsOpen => Status == NotFoundReportStatus.Open;

    public static BookNotFoundReport CreateForEdition(
        Guid id,
        UserId userId,
        Isbn13 isbn13,
        NotFoundReportLocation? location,
        string? comment,
        DateTime reportedAt)
    {
        return new BookNotFoundReport(id, userId, isbn13, null, location, comment, reportedAt);
    }

    public static BookNotFoundReport CreateForRareBook(
        Guid id,
        UserId userId,
        RareBookId rareBookId,
        NotFoundReportLocation? location,
        string? comment,
        DateTime reportedAt)
    {
        return new BookNotFoundReport(id, userId, null, rareBookId, location, comment, reportedAt);
    }

    public void Cancel(DateTime at)
    {
        Close(NotFoundReportStatus.Cancelled, null, null, at);
    }

    public void MarkFound(UserId by, string? note, DateTime at)
    {
        EnsureOpen();
        ValidateAuthor(by);
        Close(NotFoundReportStatus.Found, by, NormalizeOptionalNote(note), at);
    }

    public void MarkWithdrawn(
        UserId by,
        NotFoundWithdrawalReason reason,
        int withdrawnQuantity,
        BookMovementId? movementId,
        string note,
        DateTime at)
    {
        EnsureOpen();
        ValidateAuthor(by);

        if (!Enum.IsDefined(reason))
        {
            throw new ArgumentOutOfRangeException(nameof(reason), reason, "Unknown withdrawal reason.");
        }

        if (withdrawnQuantity < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(withdrawnQuantity), withdrawnQuantity, "Quantity cannot be negative.");
        }

        if (Isbn13 is not null && movementId is null)
        {
            throw new ArgumentNullException(nameof(movementId), "An edition withdrawal must reference its stock movement.");
        }

        if (RareBookId is not null && movementId is not null)
        {
            throw new ArgumentException("A rare book withdrawal does not create an edition stock movement.", nameof(movementId));
        }

        var utcAt = DomainTime.RequireUtc(at, nameof(at));
        var closureNote = NormalizeRequiredNote(note, nameof(note));

        Status = NotFoundReportStatus.Withdrawn;
        ClosedBy = by;
        ClosedAt = utcAt;
        ClosureNote = closureNote;
        WithdrawalReason = reason;
        WithdrawnQuantity = Isbn13 is null ? null : withdrawnQuantity;
        WithdrawalMovementId = movementId;
    }

    public void Dismiss(UserId by, string note, DateTime at)
    {
        EnsureOpen();
        ValidateAuthor(by);
        Close(NotFoundReportStatus.Dismissed, by, NormalizeRequiredNote(note, nameof(note)), at);
    }

    public void Lapse(DateTime at)
    {
        Close(NotFoundReportStatus.Lapsed, null, null, at);
    }

    public void DetachMember()
    {
        UserId = null;
        Comment = null;
    }

    private void Close(NotFoundReportStatus status, UserId? author, string? note, DateTime at)
    {
        EnsureOpen();
        var utcAt = DomainTime.RequireUtc(at, nameof(at));

        Status = status;
        ClosedBy = author;
        ClosureNote = note;
        ClosedAt = utcAt;
    }

    private void EnsureOpen()
    {
        if (!IsOpen)
        {
            throw new InvalidOperationException("Only an open report can be changed.");
        }
    }

    private static void ValidateAuthor(UserId? author)
    {
        if (author is null || author.Value == Guid.Empty)
        {
            throw new ArgumentException("A valid volunteer identifier is required.", nameof(author));
        }
    }

    private static string? NormalizeComment(string? comment)
    {
        var normalized = string.IsNullOrWhiteSpace(comment) ? null : comment.Trim();
        if (normalized is { Length: > CommentMaxLength })
        {
            throw new ArgumentException($"A report comment cannot exceed {CommentMaxLength} characters.", nameof(comment));
        }

        return normalized;
    }

    private static string? NormalizeOptionalNote(string? note)
    {
        var normalized = string.IsNullOrWhiteSpace(note) ? null : note.Trim();
        if (normalized is { Length: > ClosureNoteMaxLength })
        {
            throw new ArgumentException($"A closure note cannot exceed {ClosureNoteMaxLength} characters.", nameof(note));
        }

        return normalized;
    }

    private static string NormalizeRequiredNote(string note, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(note))
        {
            throw new ArgumentException("A closure note is required.", parameterName);
        }

        var normalized = note.Trim();
        if (normalized.Length > ClosureNoteMaxLength)
        {
            throw new ArgumentException($"A closure note cannot exceed {ClosureNoteMaxLength} characters.", parameterName);
        }

        return normalized;
    }
}
