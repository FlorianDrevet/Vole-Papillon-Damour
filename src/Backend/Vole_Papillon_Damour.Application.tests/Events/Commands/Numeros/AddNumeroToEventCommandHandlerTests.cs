using FluentAssertions;
using NSubstitute;
using Vole_Papillon_Damour.Application.Common.Interfaces.Persistence;
using Vole_Papillon_Damour.Application.Events.Commands.Numeros.AddNumeroToEvent;
using Vole_Papillon_Damour.Application.tests.Common;
using Vole_Papillon_Damour.Domain.AssoEventsAggregate;
using Vole_Papillon_Damour.Domain.AssoEventsAggregate.ValueObjects;
using Vole_Papillon_Damour.Domain.Common.Errors;
using Vole_Papillon_Damour.Domain.EventsAggregate.ValueObjects;

namespace Vole_Papillon_Damour.Application.tests.Events.Commands.Numeros;

public class AddNumeroToEventCommandHandlerTests
{
    [Fact]
    public async Task Handle_WhenEventDoesNotExist_ReturnsNotFoundAndDoesNotPersist()
    {
        var eventRepository = Substitute.For<IEventRepository>();
        var eventId = AssoEventsId.CreateUnique();
        eventRepository.GetByIdAsync(eventId).Returns(Task.FromResult<AssoEvents?>(null));
        var handler = new AddNumeroToEventCommandHandler(eventRepository, TestMapperFactory.Create());

        var result = await handler.Handle(new AddNumeroToEventCommand(eventId, 25), CancellationToken.None);

        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be(Errors.AssoEvent.AssoEventNotFound(eventId).Code);
        await eventRepository.DidNotReceive().UpdateAsync(Arg.Any<AssoEvents>());
    }

    [Fact]
    public async Task Handle_WhenCurrentPartieDoesNotExist_ReturnsPartieErrorAndDoesNotPersist()
    {
        var eventRepository = Substitute.For<IEventRepository>();
        var assoEvent = LiveTableauTestData.CreateEvent(LiveTableauTestData.CreatePartie(0));
        assoEvent.CurrentPartieIndex = 4;
        eventRepository.GetByIdAsync(assoEvent.Id).Returns(Task.FromResult<AssoEvents?>(assoEvent));
        var handler = new AddNumeroToEventCommandHandler(eventRepository, TestMapperFactory.Create());

        var result = await handler.Handle(new AddNumeroToEventCommand(assoEvent.Id, 25), CancellationToken.None);

        result.IsError.Should().BeTrue();
        await eventRepository.DidNotReceive().UpdateAsync(Arg.Any<AssoEvents>());
    }

    [Fact]
    public async Task Handle_WhenNumeroAlreadyExists_ReturnsDuplicateErrorAndDoesNotPersist()
    {
        var eventRepository = Substitute.For<IEventRepository>();
        var partie = LiveTableauTestData.CreatePartie(0);
        var assoEvent = LiveTableauTestData.CreateEvent(partie);
        partie.AddLiveNumero(25);
        eventRepository.GetByIdAsync(assoEvent.Id).Returns(Task.FromResult<AssoEvents?>(assoEvent));
        var handler = new AddNumeroToEventCommandHandler(eventRepository, TestMapperFactory.Create());

        var result = await handler.Handle(new AddNumeroToEventCommand(assoEvent.Id, 25), CancellationToken.None);

        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be(Errors.AssoEvent.Partie.NumeroAlreadyExists(assoEvent.Id, partie.Id, 25).Code);
        await eventRepository.DidNotReceive().UpdateAsync(Arg.Any<AssoEvents>());
    }

    [Fact]
    public async Task Handle_WhenStandardPartieAcceptsNumero_AddsLiveNumeroBingoNumeroAndPersists()
    {
        var eventRepository = Substitute.For<IEventRepository>();
        var partie = LiveTableauTestData.CreatePartie(0);
        var assoEvent = LiveTableauTestData.CreateEvent(partie);
        eventRepository.GetByIdAsync(assoEvent.Id).Returns(Task.FromResult<AssoEvents?>(assoEvent));
        eventRepository.UpdateAsync(Arg.Any<AssoEvents>()).Returns(callInfo => Task.FromResult(callInfo.Arg<AssoEvents>()));
        var handler = new AddNumeroToEventCommandHandler(eventRepository, TestMapperFactory.Create());

        var result = await handler.Handle(new AddNumeroToEventCommand(assoEvent.Id, 25), CancellationToken.None);

        result.IsError.Should().BeFalse();
        partie.LiveNumeros.Should().ContainSingle().Which.Should().Be(25);
        partie.LastNumeros.Should().ContainSingle().Which.Should().Be(25);
        partie.AddedBingoNumber.Should().Be(25);
        assoEvent.BingoNumeros.Should().ContainSingle().Which.Should().Be(25);
        result.Value.Parties.Single().LiveNumeros.Should().ContainSingle().Which.Should().Be(25);
        await eventRepository.Received(1).UpdateAsync(assoEvent);
    }

    [Fact]
    public async Task Handle_WhenCurrentPartieIsBingo_AddsLiveNumeroWithoutAddingPauseBingoNumero()
    {
        var eventRepository = Substitute.For<IEventRepository>();
        var partie = LiveTableauTestData.CreatePartie(0, PartieType.PartieTypeEnum.Bingo);
        var assoEvent = LiveTableauTestData.CreateEvent(partie);
        eventRepository.GetByIdAsync(assoEvent.Id).Returns(Task.FromResult<AssoEvents?>(assoEvent));
        eventRepository.UpdateAsync(Arg.Any<AssoEvents>()).Returns(callInfo => Task.FromResult(callInfo.Arg<AssoEvents>()));
        var handler = new AddNumeroToEventCommandHandler(eventRepository, TestMapperFactory.Create());

        var result = await handler.Handle(new AddNumeroToEventCommand(assoEvent.Id, 25), CancellationToken.None);

        result.IsError.Should().BeFalse();
        partie.LiveNumeros.Should().ContainSingle().Which.Should().Be(25);
        partie.AddedBingoNumber.Should().BeNull();
        assoEvent.BingoNumeros.Should().BeEmpty();
        await eventRepository.Received(1).UpdateAsync(assoEvent);
    }

    [Fact]
    public async Task Handle_WhenPartieAlreadyAddedBingoNumber_AddsLiveNumeroWithoutOverwritingPauseBingoNumero()
    {
        var eventRepository = Substitute.For<IEventRepository>();
        var partie = LiveTableauTestData.CreatePartie(0);
        var assoEvent = LiveTableauTestData.CreateEvent(partie);
        LiveTableauTestData.DrawNumeroAddingBingoNumber(assoEvent, partie, 25);
        eventRepository.GetByIdAsync(assoEvent.Id).Returns(Task.FromResult<AssoEvents?>(assoEvent));
        eventRepository.UpdateAsync(Arg.Any<AssoEvents>()).Returns(callInfo => Task.FromResult(callInfo.Arg<AssoEvents>()));
        var handler = new AddNumeroToEventCommandHandler(eventRepository, TestMapperFactory.Create());

        var result = await handler.Handle(new AddNumeroToEventCommand(assoEvent.Id, 26), CancellationToken.None);

        result.IsError.Should().BeFalse();
        partie.LiveNumeros.Should().ContainInOrder(25, 26);
        partie.LastNumeros.Should().ContainInOrder(25, 26);
        partie.AddedBingoNumber.Should().Be(25);
        assoEvent.BingoNumeros.Should().ContainSingle().Which.Should().Be(25);
        await eventRepository.Received(1).UpdateAsync(assoEvent);
    }

    [Fact]
    public async Task Handle_WhenBingoHasBeenWon_AddsLiveNumeroWithoutAddingPauseBingoNumero()
    {
        var eventRepository = Substitute.For<IEventRepository>();
        var partie = LiveTableauTestData.CreatePartie(0);
        var assoEvent = LiveTableauTestData.CreateEvent(partie);
        assoEvent.BingoHasBeenWon = true;
        eventRepository.GetByIdAsync(assoEvent.Id).Returns(Task.FromResult<AssoEvents?>(assoEvent));
        eventRepository.UpdateAsync(Arg.Any<AssoEvents>()).Returns(callInfo => Task.FromResult(callInfo.Arg<AssoEvents>()));
        var handler = new AddNumeroToEventCommandHandler(eventRepository, TestMapperFactory.Create());

        var result = await handler.Handle(new AddNumeroToEventCommand(assoEvent.Id, 25), CancellationToken.None);

        result.IsError.Should().BeFalse();
        partie.LiveNumeros.Should().ContainSingle().Which.Should().Be(25);
        partie.LastNumeros.Should().ContainSingle().Which.Should().Be(25);
        partie.AddedBingoNumber.Should().BeNull();
        assoEvent.BingoNumeros.Should().BeEmpty();
        await eventRepository.Received(1).UpdateAsync(assoEvent);
    }
}
