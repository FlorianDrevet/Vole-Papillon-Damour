using FluentAssertions;
using Vole_Papillon_Damour.Domain.AssoEventsAggregate.Entities;

namespace Vole_Papillon_Damour.Domain.tests.AssoEventsAggregateTests.Entities;

public class LotTests
{
    [Fact]
    public void IsWonByLastNumber_WhenLastNumberMatchesWonNumber_ReturnsTrue()
    {
        var lot = Lot.Create("Lot", "lot.jpg", 0, 25);

        var result = lot.IsWonByLastNumber(25);

        result.Should().BeTrue();
    }

    [Fact]
    public void IsWonByLastNumber_WhenLastNumberDoesNotMatchWonNumber_ReturnsFalse()
    {
        var lot = Lot.Create("Lot", "lot.jpg", 0, 25);

        var result = lot.IsWonByLastNumber(26);

        result.Should().BeFalse();
    }
}