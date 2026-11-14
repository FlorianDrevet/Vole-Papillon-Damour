using FluentAssertions;
using Vole_Papillon_Damour.Domain.BookAggregate.ValueObjects;
using Vole_Papillon_Damour.Domain.FavoriteBookAggregate;

namespace Vole_Papillon_Damour.Domain.tests.FavoriteBookAggregateTests;

public sealed class FavoriteBookAggregateTests
{
    private static readonly DateTime FirstSeenAt = new(2026, 11, 14, 9, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Decrement_WhenCountIsZero_KeepsCountAtZero()
    {
        var count = AnonymousFavoriteCount.Create(CreateIsbn("9782070363735"), FirstSeenAt);

        count.Decrement(FirstSeenAt.AddMinutes(1));

        count.Count.Should().Be(0);
    }

    private static Isbn13 CreateIsbn(string value)
    {
        Isbn13.TryCreate(value, out var isbn).Should().BeTrue();
        return isbn;
    }
}
