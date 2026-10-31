using FluentAssertions;
using NSubstitute;
using Vole_Papillon_Damour.Application.Common.Interfaces.Persistence;
using Vole_Papillon_Damour.Application.Common.Interfaces.Services;
using Vole_Papillon_Damour.Application.Events.Commands.Events.UpdateEvent;
using Vole_Papillon_Damour.Application.tests.Common;
using Vole_Papillon_Damour.Domain.AssoEventsAggregate;
using Vole_Papillon_Damour.Domain.AssoEventsAggregate.ValueObjects;

namespace Vole_Papillon_Damour.Application.tests.Events.Commands;

public sealed class UpdateEventLiveThemeTests
{
    [Fact]
    public async Task Handle_WhenThemeChanges_UpdatesThemeAndLiveRevision()
    {
        var (handler, eventRepository, assoEvent) = CreateFixture();
        var command = CreateCommand(assoEvent, new LiveTheme(LiveTheme.LiveThemeEnum.Halloween));

        var result = await handler.Handle(command, CancellationToken.None);

        result.Value.LiveTheme.Should().Be(new LiveTheme(LiveTheme.LiveThemeEnum.Halloween));
        result.Value.LiveRevision.Should().Be(1);
        await eventRepository.Received(1).UpdateAsync(assoEvent);
    }

    [Fact]
    public async Task Handle_WhenThemeIsUnchanged_DoesNotIncrementLiveRevision()
    {
        var (handler, eventRepository, assoEvent) = CreateFixture();
        var command = CreateCommand(assoEvent, new LiveTheme(LiveTheme.LiveThemeEnum.Classic));

        var result = await handler.Handle(command, CancellationToken.None);

        result.Value.LiveTheme.Should().Be(new LiveTheme(LiveTheme.LiveThemeEnum.Classic));
        result.Value.LiveRevision.Should().Be(0);
        await eventRepository.Received(1).UpdateAsync(assoEvent);
    }

    private static (UpdateEventCommandHandler Handler, IEventRepository Repository, AssoEvents Event) CreateFixture()
    {
        var assoEvent = LiveTableauTestData.CreateEvent();
        var eventRepository = Substitute.For<IEventRepository>();
        eventRepository.GetByIdAsync(assoEvent.Id).Returns(Task.FromResult<AssoEvents?>(assoEvent));
        eventRepository.UpdateAsync(Arg.Any<AssoEvents>())
            .Returns(callInfo => Task.FromResult(callInfo.Arg<AssoEvents>()));

        var handler = new UpdateEventCommandHandler(
            eventRepository,
            Substitute.For<IBlobService>(),
            TestMapperFactory.Create());

        return (handler, eventRepository, assoEvent);
    }

    private static UpdateEventCommand CreateCommand(AssoEvents assoEvent, LiveTheme theme) => new(
        assoEvent.Id,
        assoEvent.Name,
        null,
        assoEvent.UrlImage,
        assoEvent.EventsType,
        assoEvent.DateStart,
        assoEvent.DateEnd,
        assoEvent.HourOpenDoors,
        assoEvent.HourCloseDoors,
        assoEvent.UrlRegistration,
        null,
        assoEvent.UrlImageMap,
        assoEvent.Description,
        assoEvent.Adresse,
        theme);
}
