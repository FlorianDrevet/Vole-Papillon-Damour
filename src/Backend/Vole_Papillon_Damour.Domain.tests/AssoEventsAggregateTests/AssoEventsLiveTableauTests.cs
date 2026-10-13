using FluentAssertions;
using Vole_Papillon_Damour.Domain.AssoEventsAggregate;
using Vole_Papillon_Damour.Domain.AssoEventsAggregate.Entities;
using Vole_Papillon_Damour.Domain.AssoEventsAggregate.ValueObjects;
using Vole_Papillon_Damour.Domain.EventsAggregate.ValueObjects;

namespace Vole_Papillon_Damour.Domain.tests.AssoEventsAggregateTests;

public class AssoEventsLiveTableauTests
{
    private static readonly TimeZoneInfo Paris = TimeZoneInfo.FindSystemTimeZoneById(
        OperatingSystem.IsWindows() ? "Romance Standard Time" : "Europe/Paris");

    [Fact]
    public void MarkLiveChange_IncrementsTheRevision()
    {
        var assoEvent = CreateEvent(DateStart, null, CreatePartie(0));

        assoEvent.MarkLiveChange();
        assoEvent.MarkLiveChange();

        assoEvent.LiveRevision.Should().Be(2);
    }

    [Fact]
    public void GetLiveStatus_WithoutParties_IsNotStarted()
    {
        CreateEvent(DateStart, null).GetLiveStatus().Should().Be(LiveTableauStatus.NotStarted);
    }

    [Fact]
    public void GetLiveStatus_BeforeTheFirstNumber_IsNotStarted()
    {
        var assoEvent = CreateEvent(DateStart, null, CreatePartie(0), CreatePartie(1));

        assoEvent.GetLiveStatus().Should().Be(LiveTableauStatus.NotStarted);
        assoEvent.GetLastDrawnNumero().Should().BeNull();
    }

    [Fact]
    public void GetLiveStatus_AfterTheFirstNumber_IsLive()
    {
        var first = CreatePartie(0);
        var assoEvent = CreateEvent(DateStart, null, first, CreatePartie(1));
        first.AddLiveNumero(42);

        assoEvent.GetLiveStatus().Should().Be(LiveTableauStatus.Live);
        assoEvent.GetLastDrawnNumero().Should().Be(42);
    }

    [Fact]
    public void GetLastDrawnNumero_WhenTheCurrentPartieHasNoNumberYet_ReturnsThePreviousPartieLastNumber()
    {
        var first = CreatePartie(0);
        var assoEvent = CreateEvent(DateStart, null, first, CreatePartie(1));
        first.AddLiveNumero(12);
        first.AddLiveNumero(75);
        assoEvent.CurrentPartieIndex = 1;

        assoEvent.GetLiveStatus().Should().Be(LiveTableauStatus.Live);
        assoEvent.GetLastDrawnNumero().Should().Be(75);
    }

    [Fact]
    public void GetLiveStatus_AfterTheLastPartie_IsFinished()
    {
        var first = CreatePartie(0);
        var assoEvent = CreateEvent(DateStart, null, first);
        first.AddLiveNumero(7);
        assoEvent.CurrentPartieIndex = 1;

        assoEvent.GetLiveStatus().Should().Be(LiveTableauStatus.Finished);
        assoEvent.GetLastDrawnNumero().Should().Be(7);
    }

    [Theory]
    // Stocké à minuit UTC (date seule) : c'est bien le 15 novembre à Paris.
    [InlineData("2026-11-15T00:00:00+00:00", null, "2026-11-15", true)]
    // Stocké à minuit heure de Paris (23:00 UTC la veille).
    [InlineData("2026-11-14T23:00:00+00:00", null, "2026-11-15", true)]
    [InlineData("2026-11-15T13:00:00+00:00", null, "2026-11-14", false)]
    [InlineData("2026-11-15T13:00:00+00:00", null, "2026-11-16", false)]
    // Événement sur deux jours : le second jour compte aussi.
    [InlineData("2026-11-14T08:00:00+00:00", "2026-11-15T17:00:00+00:00", "2026-11-15", true)]
    // Fin antérieure au début (saisie erronée) : seul le jour de début compte.
    [InlineData("2026-11-15T08:00:00+00:00", "2026-11-10T17:00:00+00:00", "2026-11-15", true)]
    public void IsHappeningOn_ComparesParisCalendarDays(string start, string? end, string day, bool expected)
    {
        var assoEvent = CreateEvent(
            DateTimeOffset.Parse(start),
            end is null ? null : DateTimeOffset.Parse(end));

        assoEvent.IsHappeningOn(DateOnly.Parse(day), Paris).Should().Be(expected);
    }

    [Fact]
    public void IsHappeningOn_WhenCancelled_IsFalse()
    {
        var assoEvent = CreateEvent(DateTimeOffset.Parse("2026-11-15T13:00:00+00:00"), null);
        assoEvent.Cancel();

        assoEvent.IsHappeningOn(new DateOnly(2026, 11, 15), Paris).Should().BeFalse();
    }

    private static readonly DateTimeOffset DateStart = new(2026, 11, 15, 13, 0, 0, TimeSpan.Zero);

    private static AssoEvents CreateEvent(DateTimeOffset dateStart, DateTimeOffset? dateEnd, params Partie[] parties)
    {
        return AssoEvents.Create(
            "Grand loto",
            new Uri("https://example.com/loto.jpg"),
            new EventsType(EventsType.EventsTypeEnum.Bingo),
            dateStart,
            dateEnd,
            null,
            null,
            null,
            new Adresse(12, "Arras", "Rue du loto", 62000),
            null,
            parties,
            "Loto");
    }

    private static Partie CreatePartie(int index)
    {
        return Partie.Create(
            $"Partie {index}",
            new PartieType(PartieType.PartieTypeEnum.Standard),
            index,
            false,
            [LinePartie.Create([Lot.Create("Lot", "lot.jpg", 0)], new NumberLine(NumberLine.NumberLineEnum.OneLine), 0)]);
    }
}
