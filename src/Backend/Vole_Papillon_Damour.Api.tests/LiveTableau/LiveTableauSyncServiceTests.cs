using ErrorOr;
using FluentAssertions;
using MapsterMapper;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using Vole_Papillon_Damour.Api.LiveTableau;
using Vole_Papillon_Damour.Application.Events.Common;
using Vole_Papillon_Damour.Application.Events.Queries.GetAssoEventById;
using Vole_Papillon_Damour.Application.Events.Queries.GetLiveRevisions;
using Vole_Papillon_Damour.Contracts.Events.Responses;
using Vole_Papillon_Damour.Domain.AssoEventsAggregate;
using Vole_Papillon_Damour.Domain.EventsAggregate.ValueObjects;

namespace Vole_Papillon_Damour.Api.tests.LiveTableau;

public sealed class LiveTableauSyncServiceTests
{
    [Fact]
    public async Task SyncOnce_BringsANumberDrawnOnAnotherReplicaToThisReplicaClients()
    {
        // The database, shared by both replicas.
        var loto = LiveTableauTestEvents.CreateLoto();
        loto.MarkLiveChange();
        var mapper = LiveTableauTestEvents.CreateMapper();
        var sender = SenderReading(loto, mapper);

        // A phone is connected to replica B and knows revision 1.
        var replicaB = new LiveTableauHub();
        using var phone = replicaB.Subscribe(loto.Id);
        replicaB.ObserveRevision(loto.Id, loto.LiveRevision);

        // The operator draws 42 through replica A.
        var replicaA = new LiveTableauHub();
        loto.Parties![0].AddLiveNumero(42);
        loto.MarkLiveChange();
        replicaA.Publish(loto.Id, LiveTableauSnapshot.From(mapper.Map<EventResponse>(mapper.Map<AssoEventResult>(loto))));
        phone.Reader.TryRead(out _).Should().BeFalse();

        await CreateService(replicaB, sender, mapper).SyncOnceAsync(CancellationToken.None);

        phone.Reader.TryRead(out var snapshot).Should().BeTrue();
        snapshot!.Revision.Should().Be(2);
        snapshot.Json.Should().Contain("\"liveNumeros\":[42]");
    }

    [Fact]
    public async Task SyncOnce_WithoutConnectedClient_DoesNotQueryTheDatabase()
    {
        var sender = Substitute.For<ISender>();

        await CreateService(new LiveTableauHub(), sender, LiveTableauTestEvents.CreateMapper())
            .SyncOnceAsync(CancellationToken.None);

        await sender.DidNotReceiveWithAnyArgs().Send(Arg.Any<GetLiveRevisionsQuery>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SyncOnce_WhenTheRevisionDidNotMove_DoesNotReloadTheEvent()
    {
        var loto = LiveTableauTestEvents.CreateLoto();
        loto.MarkLiveChange();
        var mapper = LiveTableauTestEvents.CreateMapper();
        var sender = SenderReading(loto, mapper);
        var hub = new LiveTableauHub();
        using var phone = hub.Subscribe(loto.Id);
        hub.ObserveRevision(loto.Id, loto.LiveRevision);

        await CreateService(hub, sender, mapper).SyncOnceAsync(CancellationToken.None);

        await sender.DidNotReceive().Send(Arg.Any<GetAssoEventByIdQuery>(), Arg.Any<CancellationToken>());
        phone.Reader.TryRead(out _).Should().BeFalse();
    }

    private static ISender SenderReading(AssoEvents loto, IMapper mapper)
    {
        var sender = Substitute.For<ISender>();
        sender.Send(Arg.Any<GetLiveRevisionsQuery>(), Arg.Any<CancellationToken>())
            .Returns(_ => (IReadOnlyDictionary<AssoEventsId, int>)new Dictionary<AssoEventsId, int>
            {
                [loto.Id] = loto.LiveRevision
            });
        sender.Send(Arg.Any<GetAssoEventByIdQuery>(), Arg.Any<CancellationToken>())
            .Returns(_ => (ErrorOr<AssoEventResult>)mapper.Map<AssoEventResult>(loto));
        return sender;
    }

    private static LiveTableauSyncService CreateService(ILiveTableauHub hub, ISender sender, IMapper mapper)
    {
        var services = new ServiceCollection()
            .AddSingleton(sender)
            .AddSingleton(mapper)
            .BuildServiceProvider();

        return new LiveTableauSyncService(
            hub,
            services.GetRequiredService<IServiceScopeFactory>(),
            Options.Create(new LiveTableauOptions()),
            NullLogger<LiveTableauSyncService>.Instance);
    }
}
