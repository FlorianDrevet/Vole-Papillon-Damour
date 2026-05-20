using FluentAssertions;
using NSubstitute;
using Vole_Papillon_Damour.Application.Common.Interfaces.Persistence;
using Vole_Papillon_Damour.Application.Events.Commands.RemoveLastNumero;
using Vole_Papillon_Damour.Application.tests.Common;
using Vole_Papillon_Damour.Domain.AssoEventsAggregate;
using Vole_Papillon_Damour.Domain.Common.Errors;
using Vole_Papillon_Damour.Domain.EventsAggregate.ValueObjects;

namespace Vole_Papillon_Damour.Application.tests.Events.Commands.Numeros;

public class RemoveLastNumeroCommandHandlerTests
{
    [Fact]
    public async Task Handle_WhenEventDoesNotExist_ReturnsNotFoundAndDoesNotPersist()
    {
        var eventRepository = Substitute.For<IEventRepository>();
        var eventId = AssoEventsId.CreateUnique();
        eventRepository.GetByIdAsync(eventId).Returns(Task.FromResult<AssoEvents?>(null));
        var handler = new RemoveLastNumeroCommandHandler(eventRepository, TestMapperFactory.Create());

        var result = await handler.Handle(new RemoveLastNumeroCommand(eventId), CancellationToken.None);

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
        var handler = new RemoveLastNumeroCommandHandler(eventRepository, TestMapperFactory.Create());

        var result = await handler.Handle(new RemoveLastNumeroCommand(assoEvent.Id), CancellationToken.None);

        result.IsError.Should().BeFalse();
        result.Value.CurrentPartieIndex.Should().Be(-1);
        await eventRepository.DidNotReceive().UpdateAsync(Arg.Any<AssoEvents>());
    }

    [Fact]
    public async Task Handle_WhenCurrentPartieHasLastNumero_RemovesItAndPersists()
    {
        var eventRepository = Substitute.For<IEventRepository>();
        var partie = LiveTableauTestData.CreatePartie(0);
        var assoEvent = LiveTableauTestData.CreateEvent(partie);
        LiveTableauTestData.DrawNumeroAddingBingoNumber(assoEvent, partie, 25);
        eventRepository.GetByIdAsync(assoEvent.Id).Returns(Task.FromResult<AssoEvents?>(assoEvent));
        eventRepository.UpdateAsync(Arg.Any<AssoEvents>()).Returns(callInfo => Task.FromResult(callInfo.Arg<AssoEvents>()));
        var handler = new RemoveLastNumeroCommandHandler(eventRepository, TestMapperFactory.Create());

        var result = await handler.Handle(new RemoveLastNumeroCommand(assoEvent.Id), CancellationToken.None);

        result.IsError.Should().BeFalse();
        partie.LiveNumeros.Should().BeEmpty();
        partie.LastNumeros.Should().BeEmpty();
        partie.AddedBingoNumber.Should().BeNull();
        assoEvent.BingoNumeros.Should().BeEmpty();
        await eventRepository.Received(1).UpdateAsync(assoEvent);
    }

    [Fact]
    public async Task Handle_WhenCurrentPartieIsEmpty_RollsBackPreviousPartieAndRemovesItsBingoNumero()
    {
        var eventRepository = Substitute.For<IEventRepository>();
        var firstPartie = LiveTableauTestData.CreatePartie(0);
        var secondPartie = LiveTableauTestData.CreatePartie(1);
        var assoEvent = LiveTableauTestData.CreateEvent(firstPartie, secondPartie);
        LiveTableauTestData.DrawNumeroAddingBingoNumber(assoEvent, firstPartie, 40);
        assoEvent.CurrentPartieIndex = 1;
        eventRepository.GetByIdAsync(assoEvent.Id).Returns(Task.FromResult<AssoEvents?>(assoEvent));
        eventRepository.UpdateAsync(Arg.Any<AssoEvents>()).Returns(callInfo => Task.FromResult(callInfo.Arg<AssoEvents>()));
        var handler = new RemoveLastNumeroCommandHandler(eventRepository, TestMapperFactory.Create());

        var result = await handler.Handle(new RemoveLastNumeroCommand(assoEvent.Id), CancellationToken.None);

        result.IsError.Should().BeFalse();
        assoEvent.CurrentPartieIndex.Should().Be(0);
        firstPartie.LiveNumeros.Should().BeEmpty();
        firstPartie.LastNumeros.Should().BeEmpty();
        firstPartie.AddedBingoNumber.Should().BeNull();
        assoEvent.BingoNumeros.Should().BeEmpty();
        await eventRepository.Received(1).UpdateAsync(assoEvent);
    }

    [Fact]
    public async Task Handle_WhenBingoNumeroCannotBeRemoved_ReturnsErrorWithoutPersisting()
    {
        var eventRepository = Substitute.For<IEventRepository>();
        var partie = LiveTableauTestData.CreatePartie(0);
        var assoEvent = LiveTableauTestData.CreateEvent(partie);
        partie.AddLiveNumero(25);
        partie.AddedBingoNumber = 25;
        eventRepository.GetByIdAsync(assoEvent.Id).Returns(Task.FromResult<AssoEvents?>(assoEvent));
        var handler = new RemoveLastNumeroCommandHandler(eventRepository, TestMapperFactory.Create());

        var result = await handler.Handle(new RemoveLastNumeroCommand(assoEvent.Id), CancellationToken.None);

        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be(Errors.AssoEvent.CantRemoveBingoNumero(assoEvent.Id, 25).Code);
        await eventRepository.DidNotReceive().UpdateAsync(Arg.Any<AssoEvents>());
    }
}
