using AutoFixture;
using FluentAssertions;
using Vole_Papillon_Damour.Domain.AssoEventsAggregate.Entities;
using Vole_Papillon_Damour.Domain.AssoEventsAggregate.ValueObjects;

namespace Vole_Papillon_Damour.Domain.tests.AssoEventsAggregateTests.Entities;

public class RemoveLastNumeroTests
{
    private readonly Fixture _fixture;

    public RemoveLastNumeroTests()
    {
        _fixture = new Fixture();
    }

    [Fact]
    public void RemoveLastNumeroPlusUnMoinsUn_WhenNormalCase_ShouldRemove3Numeros()
    {
        // Arrange
        var partie = Partie.Create("Test Remove Numero", 
            new PartieType(PartieType.PartieTypeEnum.PlusUnMoinsUn), 
            0, _fixture.Create<bool>(),
            _fixture.Create<IList<LinePartie>>());
        
        partie.SetLastNumero(new List<int>([12,25,36]));
        partie.SetLiveNumeros(new List<int>([12, 11, 13, 25, 24, 26, 36, 35, 37]));

        const int nbLastNumeros = 2;
        const int nbLiveNumeros = 6;
        const int lastNumero = 25;
        const int lastLiveNumero = 26;
        
        // Act
        int? numeroRemoved = partie.RemoveLastNumero();
        
        // Assert
        partie.LastNumeros.Count().Should().Be(nbLastNumeros);
        partie.LiveNumeros.Count().Should().Be(nbLiveNumeros);
        partie.LastNumeros[^1].Should().Be(lastNumero);
        partie.LiveNumeros[^1].Should().Be(lastLiveNumero);

        numeroRemoved.Should().Be(36);
    }
    
    [Fact]
    public void RemoveLastNumero_WhenNoLastNumeros_ShouldReturnNull()
    {
        // Arrange
        var partie = Partie.Create("Test Remove Numero", 
            new PartieType(PartieType.PartieTypeEnum.PlusUnMoinsUn), 
            0, _fixture.Create<bool>(),
            _fixture.Create<IList<LinePartie>>());
        
        // Act
        int? numeroRemoved = partie.RemoveLastNumero();
        
        // Assert
        numeroRemoved.Should().BeNull();
    }
    
    [Fact]
    public void RemoveLastNumero_WhenWinningLotWithThisNumber_ShouldReturnDecreaseIndex()
    {
        // Arrange
        var firstLine = LinePartie.Create([Lot.Create(
                _fixture.Create<string>(),
                _fixture.Create<string>(),
                0,
                25)],
            new NumberLine(NumberLine.NumberLineEnum.OneLine));
        
        var secondLine = LinePartie.Create([Lot.Create(
                _fixture.Create<string>(),
                _fixture.Create<string>(),
                0,
                36)],
            new NumberLine(NumberLine.NumberLineEnum.TwoLine));
        
        var partie = Partie.Create("Test Remove Numero", 
            new PartieType(PartieType.PartieTypeEnum.PlusUnMoinsUn), 
            0, _fixture.Create<bool>(),
            [firstLine, secondLine]);
        
        partie.SetLastNumero([12, 35, 80, 25, 45, 10, 1, 36]);
        partie.SetLiveNumeros([12, 35, 80, 25, 45, 10, 1, 36]);
        partie.CurrentLineIndex = 1;
        
        // Act
        int? numeroRemoved = partie.RemoveLastNumero();
        
        // Assert
        numeroRemoved.Should().Be(36);
        partie.CurrentLineIndex.Should().Be(0);
        partie.LastNumeros[^1].Should().Be(1);
        partie.LiveNumeros[^1].Should().Be(1);
    }

    [Fact]
    public void RemoveLastNumero_WhenWinningNumberBelongsToPreviousLine_RemovesWinAndDecreasesIndex()
    {
        var firstLine = LinePartie.Create(
            [Lot.Create("First lot", "first-lot.jpg", 0, 25)],
            new NumberLine(NumberLine.NumberLineEnum.OneLine),
            0);
        var secondLine = LinePartie.Create(
            [Lot.Create("Second lot", "second-lot.jpg", 0)],
            new NumberLine(NumberLine.NumberLineEnum.TwoLine),
            1);
        var partie = Partie.Create(
            "Test Remove Numero",
            new PartieType(PartieType.PartieTypeEnum.Standard),
            0,
            false,
            [firstLine, secondLine]);
        partie.SetLastNumero([25]);
        partie.SetLiveNumeros([25]);
        partie.CurrentLineIndex = 1;

        var numeroRemoved = partie.RemoveLastNumero();

        numeroRemoved.Should().Be(25);
        firstLine.Lots.Single().IsWon.Should().BeNull();
        partie.CurrentLineIndex.Should().Be(0);
    }

    [Fact]
    public void RemoveLastNumero_WhenCurrentLineIndexIsPastLastLine_UsesLastExistingLine()
    {
        var linePartie = LinePartie.Create(
            [Lot.Create("Lot", "lot.jpg", 0, 25)],
            new NumberLine(NumberLine.NumberLineEnum.OneLine),
            0);
        var partie = Partie.Create(
            "Test Remove Numero",
            new PartieType(PartieType.PartieTypeEnum.Standard),
            0,
            false,
            [linePartie]);
        partie.SetLastNumero([25]);
        partie.SetLiveNumeros([25]);
        partie.CurrentLineIndex = 1;

        var numeroRemoved = partie.RemoveLastNumero();

        numeroRemoved.Should().Be(25);
        linePartie.Lots.Single().IsWon.Should().BeNull();
        partie.CurrentLineIndex.Should().Be(0);
    }

    [Fact]
    public void RemoveLastNumero_WhenNoLineParties_RemovesLastNumeroWithoutThrowing()
    {
        var partie = Partie.Create(
            "Test Remove Numero",
            new PartieType(PartieType.PartieTypeEnum.Standard),
            0,
            false,
            []);
        partie.SetLastNumero([25]);
        partie.SetLiveNumeros([25]);

        var action = () => partie.RemoveLastNumero();

        action.Should().NotThrow().Which.Should().Be(25);
        partie.LastNumeros.Should().BeEmpty();
        partie.LiveNumeros.Should().BeEmpty();
    }

    [Fact]
    public void RemoveLastNumero_WhenLastNumeroIsMissingFromLiveState_RemovesLastNumeroWithoutThrowing()
    {
        var partie = Partie.Create(
            "Test Remove Numero",
            new PartieType(PartieType.PartieTypeEnum.Standard),
            0,
            false,
            []);
        partie.SetLastNumero([25]);
        partie.SetLiveNumeros([12, 13]);

        var action = () => partie.RemoveLastNumero();

        action.Should().NotThrow().Which.Should().Be(25);
        partie.LastNumeros.Should().BeEmpty();
        partie.LiveNumeros.Should().BeEmpty();
    }
}