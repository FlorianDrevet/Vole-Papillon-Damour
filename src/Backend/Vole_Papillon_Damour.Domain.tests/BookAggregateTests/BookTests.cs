using FluentAssertions;
using Vole_Papillon_Damour.Domain.BookAggregate;
using Vole_Papillon_Damour.Domain.BookAggregate.ValueObjects;

namespace Vole_Papillon_Damour.Domain.tests.BookAggregateTests;

public sealed class BookTests
{
    private static readonly DateTime FirstSeenAt = new(2026, 9, 3, 17, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Create_WithValidIsbn_InitializesPendingBook()
    {
        var isbn = CreateIsbn("9782070363735");

        var book = Book.Create(isbn, FirstSeenAt);

        book.Id.Should().Be(isbn);
        book.QuantityAvailable.Should().Be(0);
        book.MetadataStatus.Should().Be(BookMetadataStatus.Pending);
        book.FirstSeenAt.Should().Be(FirstSeenAt);
        book.UpdatedAt.Should().Be(FirstSeenAt);
    }

    [Fact]
    public void RedirectTo_WithDifferentCanonicalIsbn_PreservesAbsorbedBook()
    {
        var source = Book.Create(CreateIsbn("9782070363735"), FirstSeenAt);
        var canonical = CreateIsbn("9783140464079");

        source.RedirectTo(canonical);

        source.RedirectedToIsbn13.Should().Be(canonical);
    }

    [Fact]
    public void RedirectTo_WithSameIsbn_Throws()
    {
        var isbn = CreateIsbn("9782070363735");
        var book = Book.Create(isbn, FirstSeenAt);

        var action = () => book.RedirectTo(isbn);

        action.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void RecordAvailableEntry_IncrementsAvailableQuantityAndUpdatesAvailabilityDate()
    {
        var book = Book.Create(CreateIsbn("9782070363735"), FirstSeenAt);
        var occurredAt = FirstSeenAt.AddMinutes(2);

        book.RecordAvailableEntry(occurredAt);

        book.QuantityAvailable.Should().Be(1);
        book.LastAvailableAt.Should().Be(occurredAt);
        book.UpdatedAt.Should().Be(occurredAt);
    }

    [Fact]
    public void RecordAnnouncementEntry_UpdatesTimestampWithoutAddingAvailableStock()
    {
        var book = Book.Create(CreateIsbn("9782070363735"), FirstSeenAt);
        var occurredAt = FirstSeenAt.AddMinutes(2);

        book.RecordAnnouncementEntry(occurredAt);

        book.QuantityAvailable.Should().Be(0);
        book.UpdatedAt.Should().Be(occurredAt);
    }

    [Fact]
    public void RecordSale_WhenQuantityIsZero_KeepsQuantityAtZeroAndCountsTheSale()
    {
        var book = Book.Create(CreateIsbn("9782070363735"), FirstSeenAt);
        var occurredAt = FirstSeenAt.AddMinutes(2);

        book.RecordSale(occurredAt);

        book.QuantityAvailable.Should().Be(0);
        book.SalesCount.Should().Be(1);
        book.UpdatedAt.Should().Be(occurredAt);
    }

    [Fact]
    public void RecordRejection_IncrementsRejectionCountWithoutAddingToAvailableQuantity()
    {
        var book = Book.Create(CreateIsbn("9782070363735"), FirstSeenAt);

        book.RecordRejection(FirstSeenAt.AddMinutes(2));

        book.RejectionCount.Should().Be(1);
        book.QuantityAvailable.Should().Be(0);
    }

    private static Isbn13 CreateIsbn(string value)
    {
        Isbn13.TryCreate(value, out var isbn).Should().BeTrue();
        return isbn;
    }
}
