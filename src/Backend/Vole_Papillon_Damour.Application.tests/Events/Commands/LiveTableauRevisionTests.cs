using FluentAssertions;
using NSubstitute;
using Vole_Papillon_Damour.Application.Common.Interfaces.Persistence;
using Vole_Papillon_Damour.Application.Events.Commands.Events.AddBingoWin;
using Vole_Papillon_Damour.Application.Events.Commands.Events.AddWinPartie;
using Vole_Papillon_Damour.Application.Events.Commands.Numeros.AddNumeroToEvent;
using Vole_Papillon_Damour.Application.Events.Commands.RemoveLastNumero;
using Vole_Papillon_Damour.Application.tests.Common;
using Vole_Papillon_Damour.Domain.AssoEventsAggregate;

namespace Vole_Papillon_Damour.Application.tests.Events.Commands;

/// <summary>Every persisted live tableau change moves the revision sent to the public tableau.</summary>
public class LiveTableauRevisionTests
{
    [Fact]
    public async Task AddNumero_IncrementsTheRevisionReturnedAndPersisted()
    {
        var (repository, assoEvent) = CreateRepositoryWithLoto();

        var result = await new AddNumeroToEventCommandHandler(repository, TestMapperFactory.Create())
            .Handle(new AddNumeroToEventCommand(assoEvent.Id, 42), CancellationToken.None);

        result.Value.LiveRevision.Should().Be(1);
        assoEvent.LiveRevision.Should().Be(1);
    }

    [Fact]
    public async Task AddNumero_WhenRejected_KeepsTheRevision()
    {
        var (repository, assoEvent) = CreateRepositoryWithLoto();
        assoEvent.Parties![0].AddLiveNumero(42);

        var result = await new AddNumeroToEventCommandHandler(repository, TestMapperFactory.Create())
            .Handle(new AddNumeroToEventCommand(assoEvent.Id, 42), CancellationToken.None);

        result.IsError.Should().BeTrue();
        assoEvent.LiveRevision.Should().Be(0);
    }

    [Fact]
    public async Task RemoveLastNumero_IncrementsTheRevision()
    {
        var (repository, assoEvent) = CreateRepositoryWithLoto();
        assoEvent.Parties![0].AddLiveNumero(42);

        var result = await new RemoveLastNumeroCommandHandler(repository, TestMapperFactory.Create())
            .Handle(new RemoveLastNumeroCommand(assoEvent.Id), CancellationToken.None);

        result.Value.LiveRevision.Should().Be(1);
    }

    [Fact]
    public async Task AddWinPartie_IncrementsTheRevision()
    {
        var (repository, assoEvent) = CreateRepositoryWithLoto();
        assoEvent.Parties![0].AddLiveNumero(42);

        var result = await new AddWinPartieCommandHandler(repository, TestMapperFactory.Create())
            .Handle(new AddWinPartieCommand(assoEvent.Id), CancellationToken.None);

        result.Value.LiveRevision.Should().Be(1);
    }

    [Fact]
    public async Task AddBingoWin_IncrementsTheRevision()
    {
        var (repository, assoEvent) = CreateRepositoryWithLoto();

        var result = await new AddBingoWinCommandHandler(repository, TestMapperFactory.Create())
            .Handle(new AddBingoWinCommand(assoEvent.Id, true), CancellationToken.None);

        result.Value.LiveRevision.Should().Be(1);
    }

    private static (IEventRepository Repository, AssoEvents AssoEvent) CreateRepositoryWithLoto()
    {
        var assoEvent = LiveTableauTestData.CreateEvent(LiveTableauTestData.CreatePartie(0));
        var repository = Substitute.For<IEventRepository>();
        repository.GetByIdAsync(assoEvent.Id).Returns(Task.FromResult<AssoEvents?>(assoEvent));
        repository.UpdateAsync(Arg.Any<AssoEvents>()).Returns(callInfo => Task.FromResult(callInfo.Arg<AssoEvents>()));
        return (repository, assoEvent);
    }
}
