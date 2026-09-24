using Vole_Papillon_Damour.Domain.BookAggregate.ValueObjects;
using Vole_Papillon_Damour.Domain.Common;
using Vole_Papillon_Damour.Domain.Common.Models;
using Vole_Papillon_Damour.Domain.EventsAggregate.ValueObjects;
using Vole_Papillon_Damour.Domain.RareBookAggregate.Entities;
using Vole_Papillon_Damour.Domain.RareBookAggregate.ValueObjects;
using Vole_Papillon_Damour.Domain.ScanSessionAggregate.ValueObjects;
using Vole_Papillon_Damour.Domain.UserAggregate.ValueObjects;

namespace Vole_Papillon_Damour.Domain.RareBookAggregate;

public sealed class RareBook : AggregateRoot<RareBookId>
{
    public RareBookSlug Slug { get; private set; } = null!;
    public Isbn13? Isbn13 { get; private set; }
    public string Title { get; private set; } = null!;
    public string? AuthorMention { get; private set; }
    public string? Publisher { get; private set; }
    public int? PublicationYear { get; private set; }
    public decimal Price { get; private set; }
    public RareBookCondition Condition { get; private set; } = null!;
    public string? PublicDescription { get; private set; }
    public RareBookStatus Status { get; private set; } = RareBookStatus.Draft;
    public bool IsSold { get; private set; }
    public DateTime? SoldAt { get; private set; }
    public AssoEventsId? SoldAtFairId { get; private set; }
    public ScanSessionId? SoldInSessionId { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public UserId CreatedBy { get; private set; } = null!;
    public DateTime UpdatedAt { get; private set; }
    public UserId UpdatedBy { get; private set; } = null!;
    public byte[] RowVersion { get; private set; } = [];
    public Guid? ClientGestureId { get; private set; }

    private List<RareBookPhoto> _photos = [];
    public IReadOnlyList<RareBookPhoto> Photos => _photos.AsReadOnly();

    public RareBook()
    {
    }

    private RareBook(
        RareBookId id,
        string? title,
        string? authorMention,
        string? publisher,
        int? publicationYear,
        decimal price,
        RareBookCondition condition,
        string? publicDescription,
        DateTime createdAt,
        UserId createdBy,
        Isbn13? isbn13,
        int slugCollisionSuffix,
        Guid? clientGestureId) : base(id)
    {
        if (id is null || id.Value == Guid.Empty)
        {
            throw new ArgumentException("A rare book identifier is required.", nameof(id));
        }

        Title = NormalizeOptional(title, 300, nameof(title)) ?? string.Empty;
        AuthorMention = NormalizeOptional(authorMention, 300, nameof(authorMention));
        Publisher = NormalizeOptional(publisher, 200, nameof(publisher));
        PublicationYear = NormalizePublicationYear(publicationYear);
        Price = NormalizePrice(price);
        Condition = condition ?? throw new ArgumentNullException(nameof(condition));
        PublicDescription = NormalizeOptional(publicDescription, 1200, nameof(publicDescription));
        Isbn13 = ValidateIsbn(isbn13);
        ClientGestureId = ValidateClientGestureId(clientGestureId);
        Slug = RareBookSlug.Create(Title, AuthorMention, PublicationYear, slugCollisionSuffix);
        CreatedAt = DomainTime.RequireUtc(createdAt, nameof(createdAt));
        CreatedBy = EnsureUserId(createdBy);
        UpdatedAt = CreatedAt;
        UpdatedBy = CreatedBy;
        Status = RareBookStatus.Draft;
        IsSold = false;
    }

    public static RareBook CreateWithDetails(
        string title,
        string? authorMention,
        string? publisher,
        int? publicationYear,
        decimal price,
        RareBookCondition condition,
        string? publicDescription,
        DateTime createdAt,
        UserId createdBy,
        Isbn13? isbn13 = null,
        int slugCollisionSuffix = 1,
        Guid? clientGestureId = null)
    {
        return new RareBook(
            RareBookId.CreateUnique(),
            title,
            authorMention,
            publisher,
            publicationYear,
            price,
            condition,
            publicDescription,
            createdAt,
            createdBy,
            isbn13,
            slugCollisionSuffix,
            clientGestureId);
    }

    public static RareBook Create(
        string title,
        decimal price,
        DateTime createdAt,
        UserId createdBy,
        string? authorMention = null,
        string? publisher = null,
        int? publicationYear = null,
        RareBookCondition? condition = null,
        string? publicDescription = null,
        Isbn13? isbn13 = null,
        int slugCollisionSuffix = 1,
        Guid? clientGestureId = null)
    {
        return CreateWithDetails(
            title,
            authorMention,
            publisher,
            publicationYear,
            price,
            condition ?? RareBookCondition.AsNew,
            publicDescription,
            createdAt,
            createdBy,
            isbn13,
            slugCollisionSuffix,
            clientGestureId);
    }

    public bool Update(
        string title,
        string? authorMention,
        string? publisher,
        int? publicationYear,
        decimal price,
        RareBookCondition condition,
        string? publicDescription,
        DateTime updatedAt,
        UserId updatedBy,
        Isbn13? isbn13 = null)
    {
        var normalizedTitle = NormalizeOptional(title, 300, nameof(title)) ?? string.Empty;
        var normalizedAuthorMention = NormalizeOptional(authorMention, 300, nameof(authorMention));
        var normalizedPublisher = NormalizeOptional(publisher, 200, nameof(publisher));
        var normalizedPublicationYear = NormalizePublicationYear(publicationYear);
        var normalizedPrice = NormalizePrice(price);
        var normalizedPublicDescription = NormalizeOptional(publicDescription, 1200, nameof(publicDescription));
        var normalizedIsbn13 = ValidateIsbn(isbn13);

        if (Status == RareBookStatus.Published &&
            (string.IsNullOrWhiteSpace(normalizedTitle) || normalizedPrice <= 0))
        {
            return false;
        }

        if (condition is null)
        {
            throw new ArgumentNullException(nameof(condition));
        }

        var changed = Title != normalizedTitle ||
                      AuthorMention != normalizedAuthorMention ||
                      Publisher != normalizedPublisher ||
                      PublicationYear != normalizedPublicationYear ||
                      Price != normalizedPrice ||
                      Condition != condition ||
                      PublicDescription != normalizedPublicDescription ||
                      Isbn13 != normalizedIsbn13;

        if (!changed)
        {
            return false;
        }

        Title = normalizedTitle;
        AuthorMention = normalizedAuthorMention;
        Publisher = normalizedPublisher;
        PublicationYear = normalizedPublicationYear;
        Price = normalizedPrice;
        Condition = condition;
        PublicDescription = normalizedPublicDescription;
        Isbn13 = normalizedIsbn13;
        Touch(updatedAt, updatedBy);
        return true;
    }

    public bool Publish(DateTime updatedAt) => Publish(UpdatedBy ?? CreatedBy, updatedAt);

    public bool Publish(UserId updatedBy, DateTime updatedAt)
    {
        if (Status == RareBookStatus.Published)
        {
            return false;
        }

        if (string.IsNullOrWhiteSpace(Title) || Price <= 0)
        {
            return false;
        }

        Status = RareBookStatus.Published;
        Touch(updatedAt, updatedBy);
        return true;
    }

    public bool Publish(DateTime updatedAt, UserId updatedBy) => Publish(updatedBy, updatedAt);

    public bool Unpublish(DateTime updatedAt) => Unpublish(updatedAt, UpdatedBy ?? CreatedBy);

    public bool Unpublish(DateTime updatedAt, UserId updatedBy)
    {
        if (Status == RareBookStatus.Draft)
        {
            return false;
        }

        Status = RareBookStatus.Draft;
        Touch(updatedAt, updatedBy);
        return true;
    }

    public bool MarkSold(
        DateTime soldAt,
        AssoEventsId? soldAtFairId = null,
        ScanSessionId? soldInSessionId = null)
    {
        return MarkSold(soldAt, soldAtFairId, soldInSessionId, UpdatedBy ?? CreatedBy);
    }

    public bool MarkSold(
        DateTime soldAt,
        AssoEventsId? soldAtFairId,
        ScanSessionId? soldInSessionId,
        UserId updatedBy)
    {
        return MarkSold(soldAt, soldAtFairId, soldInSessionId, soldAt, updatedBy);
    }

    public bool MarkSold(
        DateTime soldAt,
        AssoEventsId? soldAtFairId,
        ScanSessionId? soldInSessionId,
        DateTime updatedAt,
        UserId updatedBy)
    {
        if (Status != RareBookStatus.Published)
        {
            throw new InvalidOperationException("A draft rare book cannot be marked sold.");
        }

        if (IsSold)
        {
            return false;
        }

        var utcSoldAt = DomainTime.RequireUtc(soldAt, nameof(soldAt));
        var utcUpdatedAt = DomainTime.RequireUtc(updatedAt, nameof(updatedAt));
        IsSold = true;
        SoldAt = utcSoldAt;
        SoldAtFairId = soldAtFairId;
        SoldInSessionId = soldInSessionId;
        Touch(utcUpdatedAt, updatedBy);
        return true;
    }

    public bool RestoreAvailability(DateTime restoredAt, UserId updatedBy)
    {
        var utcRestoredAt = DomainTime.RequireUtc(restoredAt, nameof(restoredAt));
        if (!IsSold)
        {
            return false;
        }

        if (SoldAt is null ||
            utcRestoredAt < SoldAt.Value ||
            utcRestoredAt - SoldAt.Value > TimeSpan.FromSeconds(30))
        {
            throw new InvalidOperationException(
                "A rare book can only be restored within thirty seconds of being sold.");
        }

        IsSold = false;
        SoldAt = null;
        SoldAtFairId = null;
        SoldInSessionId = null;
        Touch(utcRestoredAt, updatedBy);
        return true;
    }

    public bool AddPhoto(RareBookPhoto photo) =>
        AddPhoto(photo, photo?.UploadedAt ?? UpdatedAt, UpdatedBy ?? CreatedBy);

    public bool AddPhoto(RareBookPhoto photo, DateTime updatedAt) =>
        AddPhoto(photo, updatedAt, UpdatedBy ?? CreatedBy);

    public bool AddPhoto(RareBookPhoto photo, DateTime updatedAt, UserId updatedBy)
    {
        ArgumentNullException.ThrowIfNull(photo);
        EnsurePhotoBelongsToAggregate(photo);

        if (_photos.Any(existing => existing.Id == photo.Id))
        {
            throw new InvalidOperationException("A rare book cannot contain the same photo twice.");
        }

        var utcUpdatedAt = DomainTime.RequireUtc(updatedAt, nameof(updatedAt));
        photo.SetPosition(_photos.Count);
        _photos.Add(photo);
        Touch(utcUpdatedAt, updatedBy);
        return true;
    }

    public bool RemovePhoto(RareBookPhotoId photoId, DateTime updatedAt) =>
        RemovePhoto(photoId, updatedAt, UpdatedBy ?? CreatedBy);

    public bool RemovePhoto(RareBookPhotoId photoId, DateTime updatedAt, UserId updatedBy)
    {
        ArgumentNullException.ThrowIfNull(photoId);
        var photo = _photos.FirstOrDefault(existing => existing.Id == photoId);
        if (photo is null)
        {
            return false;
        }

        var utcUpdatedAt = DomainTime.RequireUtc(updatedAt, nameof(updatedAt));
        _photos.Remove(photo);
        NormalizePhotoPositions();
        Touch(utcUpdatedAt, updatedBy);
        return true;
    }

    public bool UpdatePhotoCaption(
        RareBookPhotoId photoId,
        string? caption,
        DateTime updatedAt,
        UserId updatedBy)
    {
        ArgumentNullException.ThrowIfNull(photoId);
        var photo = _photos.FirstOrDefault(existing => existing.Id == photoId);
        if (photo is null)
        {
            return false;
        }

        if (!photo.UpdateCaption(caption))
        {
            return false;
        }

        Touch(updatedAt, updatedBy);
        return true;
    }

    public bool ReorderPhotos(IReadOnlyList<RareBookPhotoId> orderedPhotoIds)
    {
        return ReorderPhotos(orderedPhotoIds, UpdatedAt, UpdatedBy ?? CreatedBy);
    }

    public bool ReorderPhotos(IReadOnlyList<RareBookPhotoId> orderedPhotoIds, DateTime updatedAt) =>
        ReorderPhotos(orderedPhotoIds, updatedAt, UpdatedBy ?? CreatedBy);

    public bool ReorderPhotos(
        IReadOnlyList<RareBookPhotoId> orderedPhotoIds,
        DateTime updatedAt,
        UserId updatedBy)
    {
        ArgumentNullException.ThrowIfNull(orderedPhotoIds);
        if (orderedPhotoIds.Count != _photos.Count)
        {
            throw new ArgumentException(
                "Photo reordering must contain every existing photo exactly once.",
                nameof(orderedPhotoIds));
        }

        if (orderedPhotoIds.Any(id => id is null) ||
            orderedPhotoIds.Distinct().Count() != orderedPhotoIds.Count)
        {
            throw new ArgumentException(
                "Photo reordering cannot contain duplicate or empty identifiers.",
                nameof(orderedPhotoIds));
        }

        var photosById = _photos.ToDictionary(photo => photo.Id);
        if (orderedPhotoIds.Any(id => !photosById.ContainsKey(id)))
        {
            throw new ArgumentException(
                "Photo reordering must use the identifiers of this rare book.",
                nameof(orderedPhotoIds));
        }

        var changed = orderedPhotoIds.Select((id, index) => photosById[id] != _photos[index])
            .Any(value => value);
        if (!changed)
        {
            return false;
        }

        var utcUpdatedAt = DomainTime.RequireUtc(updatedAt, nameof(updatedAt));
        _photos = orderedPhotoIds.Select(id => photosById[id]).ToList();
        NormalizePhotoPositions();
        Touch(utcUpdatedAt, updatedBy);
        return true;
    }

    private void Touch(DateTime updatedAt, UserId updatedBy)
    {
        UpdatedAt = DomainTime.RequireUtc(updatedAt, nameof(updatedAt));
        UpdatedBy = EnsureUserId(updatedBy);
    }

    private void EnsurePhotoBelongsToAggregate(RareBookPhoto photo)
    {
        if (photo.RareBookId != Id)
        {
            throw new ArgumentException(
                "The photo belongs to another rare book.",
                nameof(photo));
        }
    }

    private void NormalizePhotoPositions()
    {
        for (var index = 0; index < _photos.Count; index++)
        {
            _photos[index].SetPosition(index);
        }
    }

    private static Isbn13? ValidateIsbn(Isbn13? isbn13)
    {
        if (isbn13 is { } value && string.IsNullOrWhiteSpace(value.Value))
        {
            throw new ArgumentException("A valid ISBN-13 is required.", nameof(isbn13));
        }

        return isbn13;
    }

    private static Guid? ValidateClientGestureId(Guid? clientGestureId)
    {
        if (clientGestureId == Guid.Empty)
        {
            throw new ArgumentException(
                "A client gesture identifier must be non-empty when provided.",
                nameof(clientGestureId));
        }

        return clientGestureId;
    }

    private static UserId EnsureUserId(UserId? userId)
    {
        if (userId is null || userId.Value == Guid.Empty)
        {
            throw new ArgumentException("A valid user identifier is required.", nameof(userId));
        }

        return userId;
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

    private static decimal NormalizePrice(decimal value)
    {
        if (value < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(value), "The rare book price cannot be negative.");
        }

        if (value > 99_999_999.99m || decimal.Round(value, 2) != value)
        {
            throw new ArgumentOutOfRangeException(
                nameof(value),
                "The rare book price must fit decimal(10,2).");
        }

        return value;
    }

    private static string? NormalizeOptional(string? value, int maxLength, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var normalized = value.Trim();
        if (normalized.Length > maxLength)
        {
            throw new ArgumentException(
                $"The value cannot exceed {maxLength} characters.",
                parameterName);
        }

        return normalized;
    }
}
