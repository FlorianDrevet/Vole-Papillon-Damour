using FluentAssertions;
using Vole_Papillon_Damour.Domain.AssoEventsAggregate;
using Vole_Papillon_Damour.Domain.AssoEventsAggregate.Entities;
using Vole_Papillon_Damour.Domain.AssoEventsAggregate.ValueObjects;
using Vole_Papillon_Damour.Domain.EventsAggregate.ValueObjects;

namespace Vole_Papillon_Damour.Domain.tests.AssoEventsAggregateTests;

public class AssoEventsTests
{
    [Fact]
    public void AddBingoNumero_WhenNumeroIsNewAndBingoIsNotWon_AddsNumeroAndReturnsTrue()
    {
        var assoEvent = CreateEvent();

        var result = assoEvent.AddBingoNumero(25);

        result.Should().BeTrue();
        assoEvent.BingoNumeros.Should().ContainSingle().Which.Should().Be(25);
    }

    [Fact]
    public void AddBingoNumero_WhenNumeroAlreadyExists_ReturnsFalseWithoutDuplicatingNumero()
    {
        var assoEvent = CreateEvent();
        assoEvent.AddBingoNumero(25);

        var result = assoEvent.AddBingoNumero(25);

        result.Should().BeFalse();
        assoEvent.BingoNumeros.Should().ContainSingle().Which.Should().Be(25);
    }

    [Fact]
    public void AddBingoNumero_WhenBingoHasBeenWon_ReturnsFalseWithoutAddingNumero()
    {
        var assoEvent = CreateEvent();
        assoEvent.BingoHasBeenWon = true;

        var result = assoEvent.AddBingoNumero(25);

        result.Should().BeFalse();
        assoEvent.BingoNumeros.Should().BeEmpty();
    }

    [Fact]
    public void RemoveBingoNumero_WhenNumeroExists_RemovesNumeroAndReturnsTrue()
    {
        var assoEvent = CreateEvent();
        assoEvent.AddBingoNumero(25);

        var result = assoEvent.RemoveBingoNumero(25);

        result.Should().BeTrue();
        assoEvent.BingoNumeros.Should().BeEmpty();
    }

    [Fact]
    public void RemoveBingoNumero_WhenNumeroDoesNotExist_ReturnsFalse()
    {
        var assoEvent = CreateEvent();

        var result = assoEvent.RemoveBingoNumero(25);

        result.Should().BeFalse();
    }

    [Fact]
    public void Cancel_MarksEventAsCancelledAndIsIdempotent()
    {
        var assoEvent = CreateEvent();

        assoEvent.Cancel().Should().BeTrue();
        assoEvent.IsCancelled.Should().BeTrue();
        assoEvent.Cancel().Should().BeFalse();
    }

    private static AssoEvents CreateEvent(params Partie[] parties)
    {
        return AssoEvents.Create(
            "Live loto",
            new Uri("https://example.com/loto.jpg"),
            new EventsType(EventsType.EventsTypeEnum.Bingo),
            new DateTimeOffset(2026, 5, 19, 20, 0, 0, TimeSpan.Zero),
            null,
            null,
            null,
            null,
            new Adresse(12, "Arras", "Rue du loto", 62000),
            null,
            parties,
            "Live loto test event");
    }
}
