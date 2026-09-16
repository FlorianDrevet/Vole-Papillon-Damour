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
    public RareBookShelf Shelf { get; private set; } = null!;
    public decimal Price { get; private set; }
    public RareBookCondition Condition { get; private set; } = null!;
    public string? PublicDescription { get; private set; }
    public string? Binding { get; private set; }
    public string? Dimensions { get; private set; }
    public int? PageCount { get; private set; }
    public string? ShelfLocation { get; private set; }
    public RareBookStatus Status { get; private set; } = RareBookStatus.Draft;
    public bool IsSold { get; private set; }
    public DateTime? SoldAt { get; private set; }
    public AssoEventsId? SoldAtFairId { get; private set; }
    public ScanSessionId? SoldInSessionId { get; private set; }
    public string? PriceSetBy { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public UserId CreatedBy { get; private set; } = null!;
    public DateTime UpdatedAt { get; private set; }
    public UserId UpdatedBy { get; private set; } = null!;
    public byte[] RowVersion { get; private set; } = [];

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
        RareBookShelf shelf,
        decimal price,
        RareBookCondition condition,
        string? publicDescription,
        string? binding,
        string? dimensions,
        int? pageCount,
        string? shelfLocation,
        string? priceSetBy,
        DateTime createdAt,
        UserId createdBy,
        Isbn13? isbn13) : base(id)
    {
        if (id is null || id.Value == Guid.Empty)
        {
            throw new ArgumentException("A rare book identifier is required.", nameof(id));
        }

        Title = NormalizeOptional(title, 300, nameof(title)) ?? string.Empty;
        AuthorMention = NormalizeOptional(authorMention, 300, nameof(authorMention));
        Publisher = NormalizeOptional(publisher, 200, nameof(publisher));
        PublicationYear = NormalizePublicationYear(publicationYear);
        Shelf = shelf ?? throw new ArgumentNullException(nameof(shelf));
        Price = NormalizePrice(price);
        Condition = condition ?? throw new ArgumentNullException(nameof(condition));
        PublicDescription = NormalizeOptional(publicDescription, 1200, nameof(publicDescription));
        Binding = NormalizeOptional(binding, 120, nameof(binding));
        Dimensions = NormalizeOptional(dimensions, 60, nameof(dimensions));
        PageCount = NormalizePageCount(pageCount);
        ShelfLocation = NormalizeOptional(shelfLocation, 120, nameof(shelfLocation));
        PriceSetBy = NormalizeOptional(priceSetBy, 120, nameof(priceSetBy));
        Isbn13 = ValidateIsbn(isbn13);
        Slug = RareBookSlug.Create(Title, AuthorMention, PublicationYear);
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
        RareBookShelf shelf,
        decimal price,
        RareBookCondition condition,
        string? publicDescription,
        string? binding,
        string? dimensions,
        int? pageCount,
        string? shelfLocation,
        string? priceSetBy,
        DateTime createdAt,
        UserId createdBy,
        Isbn13? isbn13 = null)
    {
        return new RareBook(
            RareBookId.CreateUnique(),
            title,
            authorMention,
            publisher,
            publicationYear,
            shelf,
            price,
            condition,
            publicDescription,
            binding,
            dimensions,
            pageCount,
            shelfLocation,
            priceSetBy,
            createdAt,
            createdBy,
            isbn13);
    }

    public static RareBook Create(
        string title,
        decimal price,
        DateTime createdAt,
        UserId createdBy,
        string? authorMention = null,
        string? publisher = null,
        int? publicationYear = null,
        RareBookShelf? shelf = null,
        RareBookCondition? condition = null,
        string? publicDescription = null,
        string? binding = null,
        string? dimensions = null,
        int? pageCount = null,
        string? shelfLocation = null,
        string? priceSetBy = null,
        Isbn13? isbn13 = null)
    {
        return CreateWithDetails(
            title,
            authorMention,
            publisher,
            publicationYear,
            shelf ?? RareBookShelf.AncientEditions,
            price,
            condition ?? RareBookCondition.AsNew,
            publicDescription,
            binding,
            dimensions,
            pageCount,
            shelfLocation,
            priceSetBy,
            createdAt,
            createdBy,
            isbn13);
    }

    public bool Update(
        string title,
        string? authorMention,
        string? publisher,
        int? publicationYear,
        RareBookShelf shelf,
        decimal price,
        RareBookCondition condition,
        string? publicDescription,
        string? binding,
        string? dimensions,
        int? pageCount,
        string? shelfLocation,
        string? priceSetBy,
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
        var normalizedBinding = NormalizeOptional(binding, 120, nameof(binding));
        var normalizedDimensions = NormalizeOptional(dimensions, 60, nameof(dimensions));
        var normalizedPageCount = NormalizePageCount(pageCount);
        var normalizedShelfLocation = NormalizeOptional(shelfLocation, 120, nameof(shelfLocation));
        var normalizedPriceSetBy = NormalizeOptional(priceSetBy, 120, nameof(priceSetBy));
        var normalizedIsbn13 = ValidateIsbn(isbn13);

        if (Status == RareBookStatus.Published &&
            (string.IsNullOrWhiteSpace(normalizedTitle) || normalizedPrice <= 0))
        {
            return false;
        }

        if (shelf is null)
        {
            throw new ArgumentNullException(nameof(shelf));
        }

        if (condition is null)
        {
            throw new ArgumentNullException(nameof(condition));
        }

        var changed = Title != normalizedTitle ||
                      AuthorMention != normalizedAuthorMention ||
                      Publisher != normalizedPublisher ||
                      PublicationYear != normalizedPublicationYear ||
                      Shelf != shelf ||
                      Price != normalizedPrice ||
                      Condition != condition ||
                      PublicDescription != normalizedPublicDescription ||
                      Binding != normalizedBinding ||
                      Dimensions != normalizedDimensions ||
                      PageCount != normalizedPageCount ||
                      ShelfLocation != normalizedShelfLocation ||
                      PriceSetBy != normalizedPriceSetBy ||
                      Isbn13 != normalizedIsbn13;

        if (!changed)
        {
            return false;
        }

        Title = normalizedTitle;
        AuthorMention = normalizedAuthorMention;
        Publisher = normalizedPublisher;
        PublicationYear = normalizedPublicationYear;
        Shelf = shelf;
        Price = normalizedPrice;
        Condition = condition;
        PublicDescription = normalizedPublicDescription;
        Binding = normalizedBinding;
        Dimensions = normalizedDimensions;
        PageCount = normalizedPageCount;
        ShelfLocation = normalizedShelfLocation;
        PriceSetBy = normalizedPriceSetBy;
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
        if (Status != RareBookStatus.Published)
        {
            throw new InvalidOperationException("A draft rare book cannot be marked sold.");
        }

        if (IsSold)
        {
            return false;
        }

        var utcSoldAt = DomainTime.RequireUtc(soldAt, nameof(soldAt));
        IsSold = true;
        SoldAt = utcSoldAt;
        SoldAtFairId = soldAtFairId;
        SoldInSessionId = soldInSessionId;
        Touch(utcSoldAt, updatedBy);
        return true;
    }

    public bool AddPhoto(RareBookPhoto photo) => AddPhoto(photo, photo?.UploadedAt ?? UpdatedAt);

    public bool AddPhoto(RareBookPhoto photo, DateTime updatedAt)
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
        UpdatedAt = utcUpdatedAt;
        return true;
    }

    public bool RemovePhoto(RareBookPhotoId photoId, DateTime updatedAt)
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
        UpdatedAt = utcUpdatedAt;
        return true;
    }

    public bool ReorderPhotos(IReadOnlyList<RareBookPhotoId> orderedPhotoIds)
    {
        return ReorderPhotos(orderedPhotoIds, UpdatedAt);
    }

    public bool ReorderPhotos(IReadOnlyList<RareBookPhotoId> orderedPhotoIds, DateTime updatedAt)
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
        UpdatedAt = utcUpdatedAt;
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

    private static UserId EnsureUserId(UserId? userId)
    {
        if (userId is null || userId.Value == Guid.Empty)
        {
            throw new ArgumentException("A valid user identifier is required.", nameof(userId));
        }

        return userId;
    }

    private static int? NormalizePageCount(int? value)
    {
        if (value is <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(value), "The page count must be positive.");
        }

        return value;
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
