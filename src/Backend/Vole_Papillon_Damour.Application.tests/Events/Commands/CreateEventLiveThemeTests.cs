using FluentAssertions;
using NSubstitute;
using Vole_Papillon_Damour.Application.Common.Interfaces.Persistence;
using Vole_Papillon_Damour.Application.Common.Interfaces.Services;
using Vole_Papillon_Damour.Application.Events.Commands.CreateEvent;
using Vole_Papillon_Damour.Application.tests.Common;
using Vole_Papillon_Damour.Domain.AssoEventsAggregate;
using Vole_Papillon_Damour.Domain.AssoEventsAggregate.ValueObjects;
using Vole_Papillon_Damour.Domain.EventsAggregate.ValueObjects;

namespace Vole_Papillon_Damour.Application.tests.Events.Commands;

public sealed class CreateEventLiveThemeTests
{
    [Fact]
    public async Task Handle_WhenHalloweenIsSelected_CreatesEventWithHalloweenTheme()
    {
        var eventRepository = Substitute.For<IEventRepository>();
        eventRepository.AddAsync(Arg.Any<AssoEvents>())
            .Returns(callInfo => Task.FromResult(callInfo.Arg<AssoEvents>()));
        var handler = new CreateEventCommandHandler(
            eventRepository,
            Substitute.For<IBlobService>(),
            TestMapperFactory.Create());
        var command = CreateCommand() with
        {
            LiveTheme = new LiveTheme(LiveTheme.LiveThemeEnum.Halloween)
        };

        var result = await handler.Handle(command, CancellationToken.None);

        result.Value.LiveTheme.Should().Be(new LiveTheme(LiveTheme.LiveThemeEnum.Halloween));
        result.Value.LiveRevision.Should().Be(1);
        await eventRepository.Received(1).AddAsync(Arg.Is<AssoEvents>(item =>
            item.LiveTheme.Value == LiveTheme.LiveThemeEnum.Halloween && item.LiveRevision == 1));
    }

    [Fact]
    public void CreateEventCommand_WhenThemeIsNotProvided_DefaultsToClassic()
    {
        CreateCommand().LiveTheme.Should().Be(new LiveTheme(LiveTheme.LiveThemeEnum.Classic));
    }

    private static CreateEventCommand CreateCommand() => new(
        "Grand loto",
        null,
        new EventsType(EventsType.EventsTypeEnum.Bingo),
        new DateTimeOffset(2026, 11, 1, 13, 0, 0, TimeSpan.Zero),
        null,
        null,
        null,
        null,
        null,
        "Loto",
        null,
        "Arras",
        62000,
        "Rue du loto",
        null);
}
