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
        partie.LastNumeros.Last().Should().Be(lastNumero);
        partie.LiveNumeros.Last().Should().Be(lastLiveNumero);

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
        partie.LastNumeros.Last().Should().Be(1);
        partie.LiveNumeros.Last().Should().Be(1);
    }
}