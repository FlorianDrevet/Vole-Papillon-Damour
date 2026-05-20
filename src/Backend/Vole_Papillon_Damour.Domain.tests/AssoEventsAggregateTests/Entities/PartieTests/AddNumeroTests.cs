using AutoFixture;
using FluentAssertions;
using Vole_Papillon_Damour.Domain.AssoEventsAggregate.Entities;
using Vole_Papillon_Damour.Domain.AssoEventsAggregate.ValueObjects;

namespace Vole_Papillon_Damour.Domain.tests.AssoEventsAggregateTests.Entities.PartieTests;

public class PartieTests
{
    private readonly Fixture _fixture;
    
    public PartieTests()
    {
        _fixture = new Fixture();
    }

    #region PlusUnMoinsUn

    [Theory]
    [InlineData(11)]
    [InlineData(20)]
    [InlineData(80)]
    [InlineData(2)]
    public void PartieAddLiveNumeroPlusUnMoinsUn_WhenNewNumero_AddBeforeAfterAndExactNumero(int numero)
    {
        // Arrange 
        var partie = Partie.Create("Test Partie", 
            new PartieType(PartieType.PartieTypeEnum.PlusUnMoinsUn), 
            0, _fixture.Create<bool>(),
            _fixture.Create<IList<LinePartie>>());
        
        const int nbLiveNumeros = 3;
        const int indexNewNumero = 0;
        
        // Act
        partie.AddLiveNumero(numero);
        
        // Assert
        partie.LastNumeros.Last().Should().Be(numero);
        partie.LiveNumeros.Count().Should().Be(nbLiveNumeros);
        partie.LiveNumeros.Should().Contain(numero - 1);
        partie.LiveNumeros.Should().Contain(numero);
        partie.LiveNumeros.Should().Contain(numero + 1);
        
        partie.LiveNumeros.ToList().FindLastIndex(n => n.Equals(numero))
            .Should().Be(indexNewNumero);
    }
    
    [Fact]
    public void PartieAddLiveNumeroPlusUnMoinsUn_WhenNewNumeroIs1_Add90And2()
    {
        // Arrange 
        var partie = Partie.Create("Test Partie", 
            new PartieType(PartieType.PartieTypeEnum.PlusUnMoinsUn), 
            0, _fixture.Create<bool>(),
            _fixture.Create<IList<LinePartie>>());
        
        const int nbLiveNumeros = 3;
        const int newNumero = 1;
        
        // Act
        partie.AddLiveNumero(newNumero);
        
        // Assert
        partie.LastNumeros.Last().Should().Be(newNumero);
        partie.LiveNumeros.Count().Should().Be(nbLiveNumeros);
        partie.LiveNumeros.Should().Contain(90);
        partie.LiveNumeros.Should().Contain(1);
        partie.LiveNumeros.Should().Contain(2);
    }
    
    [Fact]
    public void PartieAddLiveNumeroPlusUnMoinsUn_WhenNewNumeroIs90_Add89And1()
    {
        // Arrange 
        var partie = Partie.Create("Test Partie", 
            new PartieType(PartieType.PartieTypeEnum.PlusUnMoinsUn), 
            0, _fixture.Create<bool>(),
            _fixture.Create<IList<LinePartie>>());
        
        const int nbLiveNumeros = 3;
        const int newNumero = 90;
        
        // Act
        partie.AddLiveNumero(newNumero);
        
        // Assert
        partie.LastNumeros.Last().Should().Be(newNumero);
        partie.LiveNumeros.Count().Should().Be(nbLiveNumeros);
        partie.LiveNumeros.Should().Contain(89);
        partie.LiveNumeros.Should().Contain(90);
        partie.LiveNumeros.Should().Contain(1);
    }

    [Fact]
    public void PartieAddLiveNumeroPlusUnMoinsUn_WhenOneNeighboorAlreadyExited_AddOnlyNumeroAndOtherNeighboor()
    {
        // Arrange 
        var partie = Partie.Create("Test Partie", 
            new PartieType(PartieType.PartieTypeEnum.PlusUnMoinsUn), 
            0, _fixture.Create<bool>(),
            _fixture.Create<IList<LinePartie>>());
        
        const int nbLiveNumeros = 5;
        const int nbLastNumeros = 2;
        const int indexSecondNewNumero = 3;
        
        const int firstNewNumero = 15;
        const int secondNewNumero = 17;
        
        // Act
        partie.AddLiveNumero(firstNewNumero);
        partie.AddLiveNumero(secondNewNumero);
        
        // Assert
        partie.LastNumeros.Count().Should().Be(nbLastNumeros);
        partie.LastNumeros.Last().Should().Be(secondNewNumero);
        
        partie.LiveNumeros.Count().Should().Be(nbLiveNumeros);
        
        partie.LiveNumeros.Should().Contain(14);
        partie.LiveNumeros.Should().Contain(15);
        partie.LiveNumeros.Should().Contain(16);
        partie.LiveNumeros.Should().Contain(17);
        partie.LiveNumeros.Should().Contain(18);
        
        partie.LiveNumeros.ToList().FindLastIndex(n => n.Equals(secondNewNumero))
            .Should().Be(indexSecondNewNumero);
    }

    [Fact]
    public void PartieAddLiveNumeroPlusUnMoinsUn_WhenNumeroIsANeighboor_AddOnlyNumeroAndOtherNeighboor()
    {
        // Arrange 
        var partie = Partie.Create("Test Partie", 
            new PartieType(PartieType.PartieTypeEnum.PlusUnMoinsUn), 
            0, _fixture.Create<bool>(),
            _fixture.Create<IList<LinePartie>>());
        
        const int nbLiveNumeros = 5; // 37 twice
        const int nbLastNumeros = 2;
        const int indexSecondNewNumero = 3;
        
        const int firstNewNumero = 36;
        const int secondNewNumero = 37;
        
        // Act
        partie.AddLiveNumero(firstNewNumero);
        partie.AddLiveNumero(secondNewNumero);
        
        // Assert
        partie.LastNumeros.Count().Should().Be(nbLastNumeros);
        partie.LastNumeros.Last().Should().Be(secondNewNumero);
        
        partie.LiveNumeros.Count().Should().Be(nbLiveNumeros);
        
        partie.LiveNumeros.Should().Contain(35);
        partie.LiveNumeros.Should().Contain(36);
        partie.LiveNumeros.Should().Contain(37);
        partie.LiveNumeros.Should().Contain(38);

        partie.LiveNumeros.ToList().FindLastIndex(n => n.Equals(secondNewNumero))
            .Should().Be(indexSecondNewNumero);
    }

    [Fact]
    public void PartieAddLiveNumeroPlusUnMoinsUn_WhenAllNumeroAlreadyExited_AddOnlyNumero()
    {
        // Arrange 
        var partie = Partie.Create("Test Partie", 
            new PartieType(PartieType.PartieTypeEnum.PlusUnMoinsUn), 
            0, _fixture.Create<bool>(),
            _fixture.Create<IList<LinePartie>>());
        
        const int nbLiveNumeros = 6;
        const int nbLastNumeros = 3;
        
        const int firstNewNumero = 36;
        const int secondNewNumero = 38;
        const int thirdNewNumero = 37;
        
        // Act
        partie.AddLiveNumero(firstNewNumero);
        partie.AddLiveNumero(secondNewNumero);
        partie.AddLiveNumero(thirdNewNumero);
        
        // Assert
        partie.LastNumeros.Count().Should().Be(nbLastNumeros);
        partie.LastNumeros.Last().Should().Be(thirdNewNumero);
        partie.LiveNumeros.Last().Should().Be(thirdNewNumero);
        
        partie.LiveNumeros.Count().Should().Be(nbLiveNumeros);
        
        partie.LiveNumeros.Should().Contain(35);
        partie.LiveNumeros.Should().Contain(36);
        partie.LiveNumeros.Should().Contain(37);
        partie.LiveNumeros.Should().Contain(38);
        partie.LiveNumeros.Should().Contain(39);
    }
    
    
    [Fact]
    public void PartieAddLiveNumeroPlusUnMoinsUn_WhenNumeroAlreadyExited_ShouldNotAddNumeroAndReturnFalse()
    {
        // Arrange 
        var partie = Partie.Create("Test Partie", 
            new PartieType(PartieType.PartieTypeEnum.PlusUnMoinsUn), 
            0, _fixture.Create<bool>(),
            _fixture.Create<IList<LinePartie>>());
        
        const int nbLiveNumeros = 3;
        const int nbLastNumeros = 1;
        
        const int newNumero = 36;
        
        // Act
        partie.AddLiveNumero(newNumero);
        var result = partie.AddLiveNumero(newNumero);
        
        // Assert
        partie.LastNumeros.Count().Should().Be(nbLastNumeros);
        partie.LastNumeros.Last().Should().Be(newNumero);
        partie.LiveNumeros.Count().Should().Be(nbLiveNumeros);

        result.Should().BeFalse();
    }
    #endregion

    #region OtherPartieType

    [Fact]
    public void PartieAddLiveNumeroStandard_WhenNumeroAlreadyExited_ShouldNotAddNumeroAndReturnFalse()
    {
        // Arrange 
        var partie = Partie.Create("Test Partie", 
            new PartieType(PartieType.PartieTypeEnum.Standard), 
            0, _fixture.Create<bool>(),
            _fixture.Create<IList<LinePartie>>());
        
        const int nbLiveNumeros = 1;
        const int nbLastNumeros = 1;
        
        const int newNumero = 36;
        
        // Act
        partie.AddLiveNumero(newNumero);
        var result = partie.AddLiveNumero(newNumero);
        
        // Assert
        partie.LastNumeros.Count().Should().Be(nbLastNumeros);
        partie.LastNumeros.Last().Should().Be(newNumero);
        partie.LiveNumeros.Count().Should().Be(nbLiveNumeros);

        result.Should().BeFalse();
    }

    [Fact]
    public void PartieAddLiveNumeroStandard_WhenNewNumero_ShouldAddIt()
    {
        // Arrange 
        var partie = Partie.Create("Test Partie", 
            new PartieType(PartieType.PartieTypeEnum.Standard), 
            0, _fixture.Create<bool>(),
            _fixture.Create<IList<LinePartie>>());
        
        const int nbNumeros = 5;
        var listResult = new List<bool>();
        
        // Act
        for (int i = 20; i < 20 + nbNumeros; i++)
        {
            listResult.Add(partie.AddLiveNumero(i));
        }
        
        // Assert
        partie.LastNumeros.Count().Should().Be(nbNumeros);
        partie.LiveNumeros.Count().Should().Be(nbNumeros);

        listResult.TrueForAll(l => l).Should().BeTrue();
    }

    #endregion
}