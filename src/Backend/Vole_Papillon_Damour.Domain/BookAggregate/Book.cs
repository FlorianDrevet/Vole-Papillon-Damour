using Vole_Papillon_Damour.Domain.Common;
using Vole_Papillon_Damour.Domain.Common.Models;
using Vole_Papillon_Damour.Domain.BookAggregate.ValueObjects;

namespace Vole_Papillon_Damour.Domain.BookAggregate;

public sealed class Book : AggregateRoot<Isbn13>
{
    public Isbn13 Isbn13 => Id;
    public Isbn13? RedirectedToIsbn13 { get; private set; }
    public string? WorkId { get; private set; }
    public string? Title { get; private set; }
    public string? Authors { get; private set; }
    public string? Publisher { get; private set; }
    public int? PublicationYear { get; private set; }
    public string? PhysicalFormat { get; private set; }
    public string? Language { get; private set; }
    public string? Genre { get; private set; }

    public int QuantityAvailable { get; private set; }
    public int SalesCount { get; private set; }
    public int RejectionCount { get; private set; }

    public bool IsRare { get; private set; }
    public bool IsHiddenFromCatalog { get; private set; }
    public string? CoverBlobRef { get; private set; }

    public BookMetadataStatus MetadataStatus { get; private set; }
    public BookMetadataSource? MetadataSource { get; private set; }
    public DateTime? MetadataFetchedAt { get; private set; }
    public int ResolveAttempts { get; private set; }
    public DateTime? LastAttemptAt { get; private set; }
    public string? RawPayload { get; private set; }
    public string? ManuallyEditedFields { get; private set; }

    public DateTime FirstSeenAt { get; private set; }
    public DateTime? LastAvailableAt { get; private set; }
    public DateTime UpdatedAt { get; private set; }
    public byte[] RowVersion { get; private set; } = [];

    private Book(Isbn13 isbn13, DateTime firstSeenAt) : base(isbn13)
    {
        var utcFirstSeenAt = DomainTime.RequireUtc(firstSeenAt, nameof(firstSeenAt));
        EnsureIsbn(isbn13);

        MetadataStatus = BookMetadataStatus.Pending;
        FirstSeenAt = utcFirstSeenAt;
        UpdatedAt = utcFirstSeenAt;
    }

    public static Book Create(Isbn13 isbn13, DateTime firstSeenAt)
    {
        return new Book(isbn13, firstSeenAt);
    }

    public Book()
    {
    }

    public void RedirectTo(Isbn13 canonicalIsbn13)
    {
        EnsureIsbn(canonicalIsbn13);

        if (canonicalIsbn13 == Id)
        {
            throw new ArgumentException("A book cannot redirect to itself.", nameof(canonicalIsbn13));
        }

        if (RedirectedToIsbn13 is not null)
        {
            throw new InvalidOperationException("A redirected book cannot be redirected again.");
        }

        RedirectedToIsbn13 = canonicalIsbn13;
        IsHiddenFromCatalog = true;
    }

    public void RecordAvailableEntry(DateTime occurredAt)
    {
        var utcOccurredAt = DomainTime.RequireUtc(occurredAt, nameof(occurredAt));

        QuantityAvailable++;
        LastAvailableAt = utcOccurredAt;
        UpdatedAt = utcOccurredAt;
    }

    public void RecordAnnouncementEntry(DateTime occurredAt)
    {
        UpdatedAt = DomainTime.RequireUtc(occurredAt, nameof(occurredAt));
    }

    public void RecordSale(DateTime occurredAt, int quantity = 1)
    {
        if (quantity <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(quantity), "Sale quantity must be positive.");
        }

        var utcOccurredAt = DomainTime.RequireUtc(occurredAt, nameof(occurredAt));

        QuantityAvailable = Math.Max(0, QuantityAvailable - quantity);
        SalesCount += quantity;
        UpdatedAt = utcOccurredAt;
    }

    public void ReverseSale(DateTime occurredAt, int quantity = 1)
    {
        if (quantity <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(quantity), "Sale reversal quantity must be positive.");
        }

        var utcOccurredAt = DomainTime.RequireUtc(occurredAt, nameof(occurredAt));

        QuantityAvailable += quantity;
        SalesCount = Math.Max(0, SalesCount - quantity);
        UpdatedAt = utcOccurredAt;
    }

    public int ApplyQuantityCorrection(int quantityAvailable, DateTime occurredAt)
    {
        if (quantityAvailable < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(quantityAvailable),
                "The corrected quantity cannot be negative.");
        }

        var utcOccurredAt = DomainTime.RequireUtc(occurredAt, nameof(occurredAt));
        var delta = quantityAvailable - QuantityAvailable;
        QuantityAvailable = quantityAvailable;
        UpdatedAt = utcOccurredAt;
        return delta;
    }

    public bool UpdateRareStatus(bool isRare, DateTime updatedAt)
    {
        var utcUpdatedAt = DomainTime.RequireUtc(updatedAt, nameof(updatedAt));
        var changed = IsRare != isRare;
        IsRare = isRare;
        UpdatedAt = utcUpdatedAt;
        return changed;
    }

    public bool UpdateCatalogVisibility(bool isHidden, DateTime updatedAt)
    {
        var utcUpdatedAt = DomainTime.RequireUtc(updatedAt, nameof(updatedAt));
        var changed = IsHiddenFromCatalog != isHidden;
        IsHiddenFromCatalog = isHidden;
        UpdatedAt = utcUpdatedAt;
        return changed;
    }

    public void RecordRejection(DateTime occurredAt)
    {
        var utcOccurredAt = DomainTime.RequireUtc(occurredAt, nameof(occurredAt));

        RejectionCount++;
        UpdatedAt = utcOccurredAt;
    }

    private static void EnsureIsbn(Isbn13 isbn13)
    {
        if (string.IsNullOrWhiteSpace(isbn13.Value))
        {
            throw new ArgumentException("A valid ISBN-13 is required.", nameof(isbn13));
        }
    }
}
