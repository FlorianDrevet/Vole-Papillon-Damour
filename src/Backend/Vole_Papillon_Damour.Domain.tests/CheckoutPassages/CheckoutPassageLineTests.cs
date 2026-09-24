using FluentAssertions;
using Vole_Papillon_Damour.Domain.BookAggregate;
using Vole_Papillon_Damour.Domain.BookAggregate.ValueObjects;
using Vole_Papillon_Damour.Domain.CheckoutPassageAggregate;
using Vole_Papillon_Damour.Domain.EventsAggregate.ValueObjects;
using Vole_Papillon_Damour.Domain.RareBookAggregate;
using Vole_Papillon_Damour.Domain.RareBookAggregate.ValueObjects;
using Vole_Papillon_Damour.Domain.UserAggregate.ValueObjects;

namespace Vole_Papillon_Damour.Domain.tests.CheckoutPassages;

public sealed class CheckoutPassageLineTests
{
    private static readonly DateTime Now = new(2026, 3, 14, 15, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void ForOrdinarySale_SnapshotsBookMetadataAndPositiveQuantity()
    {
        var book = TestBooks.WithMetadata("9782070612758", "Le Petit Prince", "Antoine de Saint-Exupéry", "Gallimard", 1999, "Poche");
        var movement = TestMovements.Sale(book.Id, quantity: -2);

        var line = CheckoutPassageLine.ForOrdinarySale(Guid.NewGuid(), Guid.NewGuid(), movement, book);

        line.Title.Should().Be("Le Petit Prince");
        line.Authors.Should().Be("Antoine de Saint-Exupéry");
        line.Publisher.Should().Be("Gallimard");
        line.PublicationYear.Should().Be(1999);
        line.PhysicalFormat.Should().Be("Poche");
        line.Quantity.Should().Be(2);
        line.SaleMovementId.Should().Be(movement.Id);
    }

    [Fact]
    public void ForOrdinarySale_WithoutTitle_UsesIsbnFallback()
    {
        var book = Book.Create(TestBooks.Isbn("9782070612758"), Now);
        var line = CheckoutPassageLine.ForOrdinarySale(
            Guid.NewGuid(),
            Guid.NewGuid(),
            TestMovements.Sale(book.Id, -1),
            book);

        line.Title.Should().Be("ISBN 9782070612758");
    }

    [Fact]
    public void ForRareSale_SnapshotsRareBookAndFair()
    {
        var owner = UserId.CreateUnique();
        var rareBook = RareBook.Create("Édition ancienne", 14.50m, Now, owner, authorMention: "Une autrice", publisher: "Gallimard", publicationYear: 1922);
        var fairId = AssoEventsId.CreateUnique();

        var line = CheckoutPassageLine.ForRareSale(Guid.NewGuid(), Guid.NewGuid(), rareBook, Now, fairId);

        line.RareBookId.Should().Be(rareBook.Id);
        line.SaleMovementId.Should().BeNull();
        line.Isbn13.Should().BeNull();
        line.Title.Should().Be("Édition ancienne");
        line.Authors.Should().Be("Une autrice");
        line.Publisher.Should().Be("Gallimard");
        line.PublicationYear.Should().Be(1922);
        line.PhysicalFormat.Should().BeNull();
        line.Quantity.Should().Be(1);
        line.AssoEventsId.Should().Be(fairId);
    }

    [Fact]
    public void Void_IsIdempotent()
    {
        var book = Book.Create(TestBooks.Isbn("9782070612758"), Now);
        var line = CheckoutPassageLine.ForOrdinarySale(
            Guid.NewGuid(),
            Guid.NewGuid(),
            TestMovements.Sale(book.Id, -1),
            book);
        var at = new DateTime(2026, 3, 14, 16, 0, 0, DateTimeKind.Utc);

        line.Void(at).Should().BeTrue();
        line.Void(at.AddHours(1)).Should().BeFalse();
        line.VoidedAt.Should().Be(at);
    }
}

internal static class TestBooks
{
    public static Book WithMetadata(string isbn, string title, string authors, string publisher, int publicationYear, string physicalFormat)
    {
        var book = Book.Create(Isbn(isbn), new DateTime(2026, 3, 1, 12, 0, 0, DateTimeKind.Utc));
        book.ApplyManualMetadata(
            new BookMetadataPatch(
                title,
                authors,
                publisher,
                publicationYear,
                physicalFormat,
                null,
                null,
                null,
                [BookMetadataField.Title, BookMetadataField.Authors, BookMetadataField.Publisher, BookMetadataField.PublicationYear, BookMetadataField.PhysicalFormat]),
            new DateTime(2026, 3, 2, 12, 0, 0, DateTimeKind.Utc));
        return book;
    }

    public static Isbn13 Isbn(string value)
    {
        Isbn13.TryCreate(value, out var isbn).Should().BeTrue();
        return isbn;
    }
}

internal static class TestMovements
{
    private static readonly DateTime Now = new(2026, 3, 14, 15, 0, 0, DateTimeKind.Utc);

    public static Vole_Papillon_Damour.Domain.BookMovementAggregate.BookMovement Sale(Isbn13 isbn, int quantity)
    {
        return Vole_Papillon_Damour.Domain.BookMovementAggregate.BookMovement.Create(
            Vole_Papillon_Damour.Domain.BookMovementAggregate.ValueObjects.BookMovementId.CreateUnique(),
            isbn,
            Vole_Papillon_Damour.Domain.BookMovementAggregate.ValueObjects.BookMovementType.Sale,
            quantity,
            Now,
            Now,
            clockSuspect: false,
            scanSessionId: null,
            volunteerId: null,
            assoEventsId: null,
            note: null,
            clientGestureId: Guid.NewGuid());
    }
}
