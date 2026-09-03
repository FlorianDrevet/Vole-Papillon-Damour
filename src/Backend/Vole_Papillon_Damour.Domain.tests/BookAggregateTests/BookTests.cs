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

    private static Isbn13 CreateIsbn(string value)
    {
        Isbn13.TryCreate(value, out var isbn).Should().BeTrue();
        return isbn;
    }
}
