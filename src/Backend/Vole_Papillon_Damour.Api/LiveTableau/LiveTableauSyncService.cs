using MapsterMapper;
using MediatR;
using Microsoft.Extensions.Options;
using Vole_Papillon_Damour.Application.Events.Queries.GetAssoEventById;
using Vole_Papillon_Damour.Application.Events.Queries.GetLiveRevisions;
using Vole_Papillon_Damour.Contracts.Events.Responses;

namespace Vole_Papillon_Damour.Api.LiveTableau;

/// <summary>
/// Brings changes made on another API replica to the clients of this one. It
/// only queries the database for events that have a client here, and only
/// loads an event whose revision moved.
/// </summary>
public sealed class LiveTableauSyncService(
    ILiveTableauHub hub,
    IServiceScopeFactory scopeFactory,
    IOptions<LiveTableauOptions> options,
    ILogger<LiveTableauSyncService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(options.Value.SyncInterval);
        while (await WaitForNextTickAsync(timer, stoppingToken))
        {
            try
            {
                await SyncOnceAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception exception)
            {
                logger.LogWarning(exception, "Live tableau synchronisation failed; it will retry on the next tick.");
            }
        }
    }

    public async Task SyncOnceAsync(CancellationToken cancellationToken)
    {
        var watchedEvents = hub.GetWatchedEvents();
        if (watchedEvents.Count == 0)
        {
            return;
        }

        using var scope = scopeFactory.CreateScope();
        var sender = scope.ServiceProvider.GetRequiredService<ISender>();
        var mapper = scope.ServiceProvider.GetRequiredService<IMapper>();
        var revisions = await sender.Send(new GetLiveRevisionsQuery(watchedEvents), cancellationToken);

        foreach (var (assoEventsId, revision) in revisions)
        {
            if (hub.GetKnownRevision(assoEventsId) >= revision)
            {
                continue;
            }

            var assoEvent = await sender.Send(new GetAssoEventByIdQuery(assoEventsId), cancellationToken);
            if (assoEvent.IsError)
            {
                continue;
            }

            hub.Publish(assoEventsId, LiveTableauSnapshot.From(mapper.Map<EventResponse>(assoEvent.Value)));
        }
    }

    private static async Task<bool> WaitForNextTickAsync(PeriodicTimer timer, CancellationToken stoppingToken)
    {
        try
        {
            return await timer.WaitForNextTickAsync(stoppingToken);
        }
        catch (OperationCanceledException)
        {
            return false;
        }
    }
}
