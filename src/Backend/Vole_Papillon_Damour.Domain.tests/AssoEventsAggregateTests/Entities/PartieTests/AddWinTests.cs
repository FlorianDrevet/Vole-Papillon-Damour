using FluentAssertions;
using Vole_Papillon_Damour.Domain.AssoEventsAggregate.Entities;
using Vole_Papillon_Damour.Domain.AssoEventsAggregate.ValueObjects;

namespace Vole_Papillon_Damour.Domain.tests.AssoEventsAggregateTests.Entities.PartieTests;

public class AddWinTests
{
    [Fact]
    public void AddWin_WhenNoNumeroHasBeenDrawn_ReturnsFalseWithoutThrowing()
    {
        var partie = Partie.Create(
            "Test Partie",
            new PartieType(PartieType.PartieTypeEnum.Standard),
            0,
            false,
            [LinePartie.Create([Lot.Create("Lot", "lot.jpg", 0)], new NumberLine(NumberLine.NumberLineEnum.OneLine))]);

        var result = partie.AddWin();

        result.Should().BeFalse();
        partie.CurrentLineIndex.Should().Be(0);
    }

    [Fact]
    public void AddWin_WhenCurrentLineDoesNotExist_ReturnsFalseWithoutThrowing()
    {
        var partie = CreatePartieWithOneLine();
        partie.AddLiveNumero(25);
        partie.CurrentLineIndex = 3;

        var result = partie.AddWin();

        result.Should().BeFalse();
        partie.CurrentLineIndex.Should().Be(3);
    }

    [Fact]
    public void AddWin_WhenLastNumeroAlreadyWon_ReturnsFalseWithoutAdvancingLine()
    {
        var partie = CreatePartieWithOneLine();
        partie.AddLiveNumero(25);
        partie.AddWin();
        partie.CurrentLineIndex = 0;

        var result = partie.AddWin();

        result.Should().BeFalse();
        partie.CurrentLineIndex.Should().Be(0);
    }

    [Fact]
    public void AddWin_WhenLineStillHasLotsToWin_ReturnsFalseAndKeepsCurrentLine()
    {
        var linePartie = LinePartie.Create(
            [Lot.Create("Lot 1", "lot-1.jpg", 0), Lot.Create("Lot 2", "lot-2.jpg", 1)],
            new NumberLine(NumberLine.NumberLineEnum.OneLine));
        var partie = Partie.Create(
            "Test Partie",
            new PartieType(PartieType.PartieTypeEnum.Standard),
            0,
            false,
            [linePartie]);
        partie.AddLiveNumero(25);

        var result = partie.AddWin();

        result.Should().BeFalse();
        partie.CurrentLineIndex.Should().Be(0);
    }

    [Fact]
    public void AddWin_WhenLineIsComplete_ReturnsTrueAndAdvancesAfterLastLine()
    {
        var partie = CreatePartieWithOneLine();
        partie.AddLiveNumero(25);

        var result = partie.AddWin();

        result.Should().BeTrue();
        partie.CurrentLineIndex.Should().Be(1);
    }

    [Fact]
    public void AddWin_WhenLineIsCompleteButMoreLinesRemain_ReturnsFalseAndAdvancesToNextLine()
    {
        var firstLine = LinePartie.Create(
            [Lot.Create("Lot 1", "lot-1.jpg", 0)],
            new NumberLine(NumberLine.NumberLineEnum.OneLine));
        var secondLine = LinePartie.Create(
            [Lot.Create("Lot 2", "lot-2.jpg", 0)],
            new NumberLine(NumberLine.NumberLineEnum.TwoLine));
        var partie = Partie.Create(
            "Test Partie",
            new PartieType(PartieType.PartieTypeEnum.Standard),
            0,
            false,
            [firstLine, secondLine]);
        partie.AddLiveNumero(25);

        var result = partie.AddWin();

        result.Should().BeFalse();
        partie.CurrentLineIndex.Should().Be(1);
    }

    private static Partie CreatePartieWithOneLine()
    {
        return Partie.Create(
            "Test Partie",
            new PartieType(PartieType.PartieTypeEnum.Standard),
            0,
            false,
            [LinePartie.Create([Lot.Create("Lot", "lot.jpg", 0)], new NumberLine(NumberLine.NumberLineEnum.OneLine))]);
    }
}
