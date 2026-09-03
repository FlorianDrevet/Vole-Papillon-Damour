using FluentAssertions;
using Vole_Papillon_Damour.Domain.BookAggregate.ValueObjects;

namespace Vole_Papillon_Damour.Domain.tests.BookAggregateTests.ValueObjects;

public sealed class Isbn13Tests
{
    [Fact]
    public void TryCreate_ValidIsbn10_ReturnsNormalizedIsbn13()
    {
        var created = Isbn13.TryCreate("0-306-40615-2", out var isbn13);

        created.Should().BeTrue();
        isbn13.Value.Should().Be("9780306406157");
    }

    [Fact]
    public void TryCreate_ValidIsbn13WithSeparators_ReturnsDigitsOnly()
    {
        var created = Isbn13.TryCreate("978-2-07-036373-5", out var isbn13);

        created.Should().BeTrue();
        isbn13.Value.Should().Be("9782070363735");
    }

    [Fact]
    public void TryCreate_InvalidCheckDigit_ReturnsFalse()
    {
        var created = Isbn13.TryCreate("9782070363736", out _);

        created.Should().BeFalse();
    }

    [Fact]
    public void TryCreate_NonIsbnBarcode_ReturnsFalse()
    {
        var created = Isbn13.TryCreate("4006381333931", out _);

        created.Should().BeFalse();
    }

    [Fact]
    public void TryCreate_Isbn10WithTerminalX_ReturnsNormalizedIsbn13()
    {
        var created = Isbn13.TryCreate("0-8044-2957-X", out var isbn13);

        created.Should().BeTrue();
        isbn13.Value.Should().Be("9780804429573");
    }
}
