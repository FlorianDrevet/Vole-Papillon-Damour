using FluentAssertions;
using Vole_Papillon_Damour.Domain.AssoEventsAggregate;
using Vole_Papillon_Damour.Domain.AssoEventsAggregate.Entities;
using Vole_Papillon_Damour.Domain.AssoEventsAggregate.ValueObjects;
using Vole_Papillon_Damour.Domain.EventsAggregate.ValueObjects;

namespace Vole_Papillon_Damour.Domain.tests.AssoEventsAggregateTests;

public class AssoEventsTests
{
    [Fact]
    public void Create_InitializesLiveThemeToClassic()
    {
        var assoEvent = CreateEvent();

        assoEvent.LiveTheme.Should().Be(new LiveTheme(LiveTheme.LiveThemeEnum.Classic));
    }

    [Fact]
    public void ParameterlessConstructor_InitializesLiveThemeToClassic()
    {
        new AssoEvents().LiveTheme.Should().Be(new LiveTheme(LiveTheme.LiveThemeEnum.Classic));
    }

    [Fact]
    public void SetLiveTheme_WhenThemeChanges_UpdatesThemeAndLiveRevision()
    {
        var assoEvent = CreateEvent();
        var initialRevision = assoEvent.LiveRevision;

        var changed = assoEvent.SetLiveTheme(new LiveTheme(LiveTheme.LiveThemeEnum.Halloween));

        changed.Should().BeTrue();
        assoEvent.LiveTheme.Should().Be(new LiveTheme(LiveTheme.LiveThemeEnum.Halloween));
        assoEvent.LiveRevision.Should().Be(initialRevision + 1);
    }

    [Fact]
    public void SetLiveTheme_WhenThemeIsUnchanged_DoesNotIncrementLiveRevision()
    {
        var assoEvent = CreateEvent();
        assoEvent.SetLiveTheme(new LiveTheme(LiveTheme.LiveThemeEnum.Halloween));
        var changedRevision = assoEvent.LiveRevision;

        var changed = assoEvent.SetLiveTheme(new LiveTheme(LiveTheme.LiveThemeEnum.Halloween));

        changed.Should().BeFalse();
        assoEvent.LiveRevision.Should().Be(changedRevision);
    }

    [Fact]
    public void CreateFromString_WhenValueIsUnknown_ReturnsClassic()
    {
        LiveTheme.CreateFromString("inconnu").Should().Be(new LiveTheme(LiveTheme.LiveThemeEnum.Classic));
    }

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
