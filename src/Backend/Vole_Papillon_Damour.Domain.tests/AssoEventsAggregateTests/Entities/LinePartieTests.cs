using FluentAssertions;
using Vole_Papillon_Damour.Domain.AssoEventsAggregate.Entities;
using Vole_Papillon_Damour.Domain.AssoEventsAggregate.ValueObjects;

namespace Vole_Papillon_Damour.Domain.tests.AssoEventsAggregateTests.Entities;

public class LinePartieTests
{
    [Fact]
    public void AddWin_WhenNoLotsRemainToWin_ReturnsFalse()
    {
        var linePartie = LinePartie.Create([], new NumberLine(NumberLine.NumberLineEnum.OneLine));

        var result = linePartie.AddWin(25);

        result.Should().BeFalse();
    }

    [Fact]
    public void RemoveNumero_WhenNoWinningLotMatchesProvidedNumeros_ReturnsFalse()
    {
        var linePartie = LinePartie.Create(
            [Lot.Create("Lot", "lot.jpg", 0, 25)],
            new NumberLine(NumberLine.NumberLineEnum.OneLine));

        var result = linePartie.RemoveNumero([12, 13]);

        result.Should().BeFalse();
        linePartie.Lots.Single().IsWon.Should().Be(25);
    }
}