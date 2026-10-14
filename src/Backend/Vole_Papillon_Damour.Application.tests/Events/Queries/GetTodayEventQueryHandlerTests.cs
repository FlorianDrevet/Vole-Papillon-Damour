using FluentAssertions;
using NSubstitute;
using Vole_Papillon_Damour.Application.Common.Interfaces.Persistence;
using Vole_Papillon_Damour.Application.Common.Interfaces.Services;
using Vole_Papillon_Damour.Application.Events.Queries.GetTodayEvent;
using Vole_Papillon_Damour.Domain.AssoEventsAggregate;
using Vole_Papillon_Damour.Domain.AssoEventsAggregate.Entities;
using Vole_Papillon_Damour.Domain.AssoEventsAggregate.ValueObjects;
using Vole_Papillon_Damour.Domain.EventsAggregate.ValueObjects;

namespace Vole_Papillon_Damour.Application.tests.Events.Queries;

public class GetTodayEventQueryHandlerTests
{
    // 15 novembre 2026, 10 h 00 à Paris.
    private static readonly DateTime Now = new(2026, 11, 15, 9, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task Handle_WithoutEventToday_ReturnsNull()
    {
        var result = await Handle(Now, CreateEvent("Bourse", EventsType.EventsTypeEnum.Books, "2026-11-16T08:00:00Z"));

        result.Should().BeNull();
    }

    [Fact]
    public async Task Handle_PrefersTheLotoWhenSeveralEventsHappenToday()
    {
        var bourse = CreateEvent("Bourse", EventsType.EventsTypeEnum.Books, "2026-11-15T07:00:00Z");
        var loto = CreateEvent("Grand loto", EventsType.EventsTypeEnum.Bingo, "2026-11-15T13:00:00Z");

        var result = await Handle(Now, bourse, loto);

        result!.Id.Should().Be(loto.Id);
        result.LiveStatus.Should().Be(LiveTableauStatus.NotStarted);
        result.LastNumero.Should().BeNull();
        result.City.Should().Be("Arras");
    }

    [Fact]
    public async Task Handle_ForALiveLoto_ReturnsItsLastNumber()
    {
        var loto = CreateEvent("Grand loto", EventsType.EventsTypeEnum.Bingo, "2026-11-15T13:00:00Z");
        loto.Parties![0].AddLiveNumero(42);

        var result = await Handle(Now, loto);

        result!.LiveStatus.Should().Be(LiveTableauStatus.Live);
        result.LastNumero.Should().Be(42);
    }

    [Fact]
    public async Task Handle_ForAnotherEventType_HasNoLiveStatus()
    {
        var result = await Handle(Now, CreateEvent("Bourse", EventsType.EventsTypeEnum.Books, "2026-11-15T07:00:00Z"));

        result!.Name.Should().Be("Bourse");
        result.LiveStatus.Should().BeNull();
        result.LastNumero.Should().BeNull();
    }

    [Fact]
    public async Task Handle_JustAfterParisMidnight_UsesTheParisDay()
    {
        // 23 h 30 UTC le 14 = 0 h 30 à Paris le 15 ; loto stocké à minuit heure de Paris.
        var loto = CreateEvent("Grand loto", EventsType.EventsTypeEnum.Bingo, "2026-11-14T23:00:00Z");

        var result = await Handle(new DateTime(2026, 11, 14, 23, 30, 0, DateTimeKind.Utc), loto);

        result!.Id.Should().Be(loto.Id);
    }

    [Fact]
    public async Task Handle_IgnoresACancelledEvent()
    {
        var loto = CreateEvent("Grand loto", EventsType.EventsTypeEnum.Bingo, "2026-11-15T13:00:00Z");
        loto.Cancel();

        var result = await Handle(Now, loto);

        result.Should().BeNull();
    }

    private static async Task<Vole_Papillon_Damour.Application.Events.Common.TodayEventResult?> Handle(
        DateTime utcNow,
        params AssoEvents[] events)
    {
        var repository = Substitute.For<IEventRepository>();
        repository.GetNotCancelledFromAsync(Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>())
            .Returns(events.ToList());
        var clock = Substitute.For<IDateTimeProvider>();
        clock.UtcNow.Returns(utcNow);

        return await new GetTodayEventQueryHandler(repository, clock)
            .Handle(new GetTodayEventQuery(), CancellationToken.None);
    }

    private static AssoEvents CreateEvent(string name, EventsType.EventsTypeEnum type, string dateStart)
    {
        var partie = Partie.Create(
            "Partie 1",
            new PartieType(PartieType.PartieTypeEnum.Standard),
            0,
            false,
            [LinePartie.Create([Lot.Create("Lot", "lot.jpg", 0)], new NumberLine(NumberLine.NumberLineEnum.OneLine), 0)]);

        return AssoEvents.Create(
            name,
            new Uri("https://example.com/event.jpg"),
            new EventsType(type),
            DateTimeOffset.Parse(dateStart),
            null,
            null,
            null,
            null,
            new Adresse(12, "Arras", "Rue du loto", 62000),
            null,
            [partie],
            name);
    }
}
