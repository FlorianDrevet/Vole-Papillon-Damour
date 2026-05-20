using FluentAssertions;
using NSubstitute;
using Vole_Papillon_Damour.Application.Common.Interfaces.Persistence;
using Vole_Papillon_Damour.Application.Events.Commands.Events.AddWinPartie;
using Vole_Papillon_Damour.Application.tests.Common;
using Vole_Papillon_Damour.Domain.AssoEventsAggregate;
using Vole_Papillon_Damour.Domain.AssoEventsAggregate.ValueObjects;
using Vole_Papillon_Damour.Domain.Common.Errors;
using Vole_Papillon_Damour.Domain.EventsAggregate.ValueObjects;

namespace Vole_Papillon_Damour.Application.tests.Events.Commands.Events;

public class AddWinPartieCommandHandlerTests
{
    [Fact]
    public async Task Handle_WhenEventDoesNotExist_ReturnsNotFoundAndDoesNotPersist()
    {
        var eventRepository = Substitute.For<IEventRepository>();
        var eventId = AssoEventsId.CreateUnique();
        eventRepository.GetByIdAsync(eventId).Returns(Task.FromResult<AssoEvents?>(null));
        var handler = new AddWinPartieCommandHandler(eventRepository, TestMapperFactory.Create());

        var result = await handler.Handle(new AddWinPartieCommand(eventId), CancellationToken.None);

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
        var handler = new AddWinPartieCommandHandler(eventRepository, TestMapperFactory.Create());

        var result = await handler.Handle(new AddWinPartieCommand(assoEvent.Id), CancellationToken.None);

        result.IsError.Should().BeFalse();
        result.Value.CurrentPartieIndex.Should().Be(-1);
        await eventRepository.DidNotReceive().UpdateAsync(Arg.Any<AssoEvents>());
    }

    [Fact]
    public async Task Handle_WhenPartieHasNoNumero_DoesNotThrowAndKeepsCurrentPartieIndex()
    {
        var eventRepository = Substitute.For<IEventRepository>();
        var assoEvent = LiveTableauTestData.CreateEvent(LiveTableauTestData.CreatePartie(0));
        eventRepository.GetByIdAsync(assoEvent.Id).Returns(Task.FromResult<AssoEvents?>(assoEvent));
        eventRepository.UpdateAsync(Arg.Any<AssoEvents>()).Returns(callInfo => Task.FromResult(callInfo.Arg<AssoEvents>()));
        var handler = new AddWinPartieCommandHandler(eventRepository, TestMapperFactory.Create());

        var result = await handler.Handle(new AddWinPartieCommand(assoEvent.Id), CancellationToken.None);

        result.IsError.Should().BeFalse();
        assoEvent.CurrentPartieIndex.Should().Be(0);
        await eventRepository.Received(1).UpdateAsync(assoEvent);
    }

    [Fact]
    public async Task Handle_WhenLastPartieWins_DoesNotThrowAndMovesIndexAfterLastPartie()
    {
        var eventRepository = Substitute.For<IEventRepository>();
        var partie = LiveTableauTestData.CreatePartie(0);
        partie.AddLiveNumero(42);
        var assoEvent = LiveTableauTestData.CreateEvent(partie);
        eventRepository.GetByIdAsync(assoEvent.Id).Returns(Task.FromResult<AssoEvents?>(assoEvent));
        eventRepository.UpdateAsync(Arg.Any<AssoEvents>()).Returns(callInfo => Task.FromResult(callInfo.Arg<AssoEvents>()));
        var handler = new AddWinPartieCommandHandler(eventRepository, TestMapperFactory.Create());

        var result = await handler.Handle(new AddWinPartieCommand(assoEvent.Id), CancellationToken.None);

        result.IsError.Should().BeFalse();
        assoEvent.CurrentPartieIndex.Should().Be(1);
        await eventRepository.Received(1).UpdateAsync(assoEvent);
    }

    [Fact]
    public async Task Handle_WhenNextPartieIsAlreadyWonBingo_SkipsBingoPartie()
    {
        var eventRepository = Substitute.For<IEventRepository>();
        var firstPartie = LiveTableauTestData.CreatePartie(0);
        var bingoPartie = LiveTableauTestData.CreatePartie(1, PartieType.PartieTypeEnum.Bingo);
        var nextPartie = LiveTableauTestData.CreatePartie(2);
        firstPartie.AddLiveNumero(42);
        var assoEvent = LiveTableauTestData.CreateEvent(firstPartie, bingoPartie, nextPartie);
        assoEvent.BingoHasBeenWon = true;
        eventRepository.GetByIdAsync(assoEvent.Id).Returns(Task.FromResult<AssoEvents?>(assoEvent));
        eventRepository.UpdateAsync(Arg.Any<AssoEvents>()).Returns(callInfo => Task.FromResult(callInfo.Arg<AssoEvents>()));
        var handler = new AddWinPartieCommandHandler(eventRepository, TestMapperFactory.Create());

        var result = await handler.Handle(new AddWinPartieCommand(assoEvent.Id), CancellationToken.None);

        result.IsError.Should().BeFalse();
        assoEvent.CurrentPartieIndex.Should().Be(2);
        await eventRepository.Received(1).UpdateAsync(assoEvent);
    }

    [Fact]
    public async Task Handle_WhenCurrentPartieIsBingoWithNumero_MarksBingoAsWon()
    {
        var eventRepository = Substitute.For<IEventRepository>();
        var bingoPartie = LiveTableauTestData.CreatePartie(0, PartieType.PartieTypeEnum.Bingo);
        bingoPartie.AddLiveNumero(42);
        var assoEvent = LiveTableauTestData.CreateEvent(bingoPartie);
        eventRepository.GetByIdAsync(assoEvent.Id).Returns(Task.FromResult<AssoEvents?>(assoEvent));
        eventRepository.UpdateAsync(Arg.Any<AssoEvents>()).Returns(callInfo => Task.FromResult(callInfo.Arg<AssoEvents>()));
        var handler = new AddWinPartieCommandHandler(eventRepository, TestMapperFactory.Create());

        var result = await handler.Handle(new AddWinPartieCommand(assoEvent.Id), CancellationToken.None);

        result.IsError.Should().BeFalse();
        assoEvent.BingoHasBeenWon.Should().BeTrue();
        await eventRepository.Received(1).UpdateAsync(assoEvent);
    }
}
