using FluentAssertions;
using NSubstitute;
using Vole_Papillon_Damour.Application.Common.Interfaces.Persistence;
using Vole_Papillon_Damour.Application.Events.Commands.Events.AddBingoWin;
using Vole_Papillon_Damour.Application.tests.Common;
using Vole_Papillon_Damour.Domain.AssoEventsAggregate;
using Vole_Papillon_Damour.Domain.Common.Errors;
using Vole_Papillon_Damour.Domain.EventsAggregate.ValueObjects;

namespace Vole_Papillon_Damour.Application.tests.Events.Commands.Events;

public class AddBingoWinCommandHandlerTests
{
    [Fact]
    public async Task Handle_WhenEventDoesNotExist_ReturnsNotFoundAndDoesNotPersist()
    {
        var eventRepository = Substitute.For<IEventRepository>();
        var eventId = AssoEventsId.CreateUnique();
        eventRepository.GetByIdAsync(eventId).Returns(Task.FromResult<AssoEvents?>(null));
        var handler = new AddBingoWinCommandHandler(eventRepository, TestMapperFactory.Create());

        var result = await handler.Handle(new AddBingoWinCommand(eventId, true), CancellationToken.None);

        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be(Errors.AssoEvent.AssoEventNotFound(eventId).Code);
        await eventRepository.DidNotReceive().UpdateAsync(Arg.Any<AssoEvents>());
    }

    [Fact]
    public async Task Handle_WhenCurrentPartieIndexIsInvalid_ReturnsCurrentStateAndDoesNotPersist()
    {
        var eventRepository = Substitute.For<IEventRepository>();
        var assoEvent = LiveTableauTestData.CreateEvent(LiveTableauTestData.CreatePartie(0));
        assoEvent.CurrentPartieIndex = -1;
        eventRepository.GetByIdAsync(assoEvent.Id).Returns(Task.FromResult<AssoEvents?>(assoEvent));
        var handler = new AddBingoWinCommandHandler(eventRepository, TestMapperFactory.Create());

        var result = await handler.Handle(new AddBingoWinCommand(assoEvent.Id, true), CancellationToken.None);

        result.IsError.Should().BeFalse();
        result.Value.BingoHasBeenWon.Should().BeFalse();
        await eventRepository.DidNotReceive().UpdateAsync(Arg.Any<AssoEvents>());
    }

    [Fact]
    public async Task Handle_WhenEventExists_SetsBingoWinAndPersists()
    {
        var eventRepository = Substitute.For<IEventRepository>();
        var assoEvent = LiveTableauTestData.CreateEvent(LiveTableauTestData.CreatePartie(0));
        eventRepository.GetByIdAsync(assoEvent.Id).Returns(Task.FromResult<AssoEvents?>(assoEvent));
        eventRepository.UpdateAsync(Arg.Any<AssoEvents>()).Returns(callInfo => Task.FromResult(callInfo.Arg<AssoEvents>()));
        var handler = new AddBingoWinCommandHandler(eventRepository, TestMapperFactory.Create());

        var result = await handler.Handle(new AddBingoWinCommand(assoEvent.Id, true), CancellationToken.None);

        result.IsError.Should().BeFalse();
        assoEvent.BingoHasBeenWon.Should().BeTrue();
        result.Value.BingoHasBeenWon.Should().BeTrue();
        await eventRepository.Received(1).UpdateAsync(assoEvent);
    }
}
