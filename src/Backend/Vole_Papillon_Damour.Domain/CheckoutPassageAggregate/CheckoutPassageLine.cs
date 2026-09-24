using Vole_Papillon_Damour.Domain.BookAggregate;
using Vole_Papillon_Damour.Domain.BookAggregate.ValueObjects;
using Vole_Papillon_Damour.Domain.BookMovementAggregate;
using Vole_Papillon_Damour.Domain.BookMovementAggregate.ValueObjects;
using Vole_Papillon_Damour.Domain.Common;
using Vole_Papillon_Damour.Domain.Common.Models;
using Vole_Papillon_Damour.Domain.EventsAggregate.ValueObjects;
using Vole_Papillon_Damour.Domain.RareBookAggregate;
using Vole_Papillon_Damour.Domain.RareBookAggregate.ValueObjects;

namespace Vole_Papillon_Damour.Domain.CheckoutPassageAggregate;

public sealed class CheckoutPassageLine : Entity<Guid>
{
    public Guid CheckoutPassageId { get; private set; }
    public BookMovementId? SaleMovementId { get; private set; }
    public RareBookId? RareBookId { get; private set; }
    public Isbn13? Isbn13 { get; private set; }
    public string? RequestedIsbn13 { get; private set; }
    public int Quantity { get; private set; }
    public string Title { get; private set; } = string.Empty;
    public string? Authors { get; private set; }
    public string? Publisher { get; private set; }
    public int? PublicationYear { get; private set; }
    public string? PhysicalFormat { get; private set; }
    public AssoEventsId? AssoEventsId { get; private set; }
    public DateTime OccurredAt { get; private set; }
    public DateTime? VoidedAt { get; private set; }

    private CheckoutPassageLine(
        Guid id,
        Guid checkoutPassageId,
        BookMovementId? saleMovementId,
        RareBookId? rareBookId,
        Isbn13? isbn13,
        string? requestedIsbn13,
        int quantity,
        string title,
        string? authors,
        string? publisher,
        int? publicationYear,
        string? physicalFormat,
        AssoEventsId? assoEventsId,
        DateTime occurredAt) : base(EnsureId(id))
    {
        CheckoutPassageId = EnsureId(checkoutPassageId, nameof(checkoutPassageId));
        if (quantity <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(quantity), "A checkout passage line quantity must be positive.");
        }

        SaleMovementId = saleMovementId;
        RareBookId = rareBookId;
        Isbn13 = isbn13;
        RequestedIsbn13 = requestedIsbn13;
        Quantity = quantity;
        Title = title;
        Authors = authors;
        Publisher = publisher;
        PublicationYear = publicationYear;
        PhysicalFormat = physicalFormat;
        AssoEventsId = assoEventsId;
        OccurredAt = DomainTime.RequireUtc(occurredAt, nameof(occurredAt));
    }

    public CheckoutPassageLine()
    {
    }

    public static CheckoutPassageLine ForOrdinarySale(
        Guid id,
        Guid passageId,
        BookMovement movement,
        Book book,
        string? requestedIsbn13 = null)
    {
        ArgumentNullException.ThrowIfNull(movement);
        ArgumentNullException.ThrowIfNull(book);
        string? normalizedRequestedIsbn = null;
        if (!string.IsNullOrWhiteSpace(requestedIsbn13))
        {
            if (!BookAggregate.ValueObjects.Isbn13.TryCreate(requestedIsbn13, out var requestedIsbn))
            {
                throw new ArgumentException("The requested ISBN-13 is invalid.", nameof(requestedIsbn13));
            }

            normalizedRequestedIsbn = requestedIsbn.Value;
        }

        var title = string.IsNullOrWhiteSpace(book.Title) ? $"ISBN {book.Isbn13.Value}" : book.Title;
        return new CheckoutPassageLine(
            id,
            passageId,
            movement.Id,
            null,
            book.Isbn13,
            normalizedRequestedIsbn,
            Math.Abs(movement.Quantity),
            title,
            book.Authors,
            book.Publisher,
            book.PublicationYear,
            book.PhysicalFormat,
            movement.AssoEventsId,
            movement.OccurredAt);
    }

    public static CheckoutPassageLine ForRareSale(
        Guid id,
        Guid passageId,
        RareBook rareBook,
        DateTime occurredAt,
        AssoEventsId? fairId)
    {
        ArgumentNullException.ThrowIfNull(rareBook);
        return new CheckoutPassageLine(
            id,
            passageId,
            null,
            rareBook.Id,
            rareBook.Isbn13,
            null,
            1,
            rareBook.Title,
            rareBook.AuthorMention,
            rareBook.Publisher,
            rareBook.PublicationYear,
            rareBook.Binding,
            fairId,
            occurredAt);
    }

    public bool Void(DateTime at)
    {
        if (VoidedAt is not null)
        {
            return false;
        }

        VoidedAt = DomainTime.RequireUtc(at, nameof(at));
        return true;
    }

    private static Guid EnsureId(Guid id, string parameterName = "id") => id == Guid.Empty
        ? throw new ArgumentException("A checkout passage line identifier is required.", parameterName)
        : id;
}
