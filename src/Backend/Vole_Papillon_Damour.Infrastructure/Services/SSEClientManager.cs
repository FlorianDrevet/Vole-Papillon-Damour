using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using Vole_Papillon_Damour.Application.Common.Interfaces.Services;
using Vole_Papillon_Damour.Domain.EventsAggregate.ValueObjects;

namespace Vole_Papillon_Damour.Infrastructure.Services;

public class SSEClientManager(ILogger<SSEClientManager> logger): ISSEClientManager
{
    private readonly ConcurrentDictionary<string, SseClientConnection> _clientsById = new();
    private readonly ConcurrentDictionary<string, ConcurrentDictionary<string, byte>> _clientIdsByEvent = new();

    public void AddClient(AssoEventsId assoEventsId, string clientId, StreamWriter streamWriter)
    {
        RemoveClient(clientId);

        var eventKey = ToEventKey(assoEventsId);
        var connection = new SseClientConnection(eventKey, streamWriter);
        var clientIds = _clientIdsByEvent.GetOrAdd(eventKey, _ => new ConcurrentDictionary<string, byte>());

        _clientsById[clientId] = connection;
        clientIds[clientId] = 0;
    }

    public void RemoveClient(string clientId)
    {
        if (!_clientsById.TryRemove(clientId, out var connection))
        {
            return;
        }

        if (_clientIdsByEvent.TryGetValue(connection.EventKey, out var clientIds))
        {
            clientIds.TryRemove(clientId, out _);
            if (clientIds.IsEmpty)
            {
                _clientIdsByEvent.TryRemove(connection.EventKey, out _);
            }
        }
    }

    public async Task SendToEvent(AssoEventsId assoEventsId, string message)
    {
        var eventKey = ToEventKey(assoEventsId);
        if (!_clientIdsByEvent.TryGetValue(eventKey, out var clientIds))
        {
            return;
        }

        foreach (var clientId in clientIds.Keys.ToArray())
        {
            await SendToClient(clientId, message);
        }
    }

    public async Task SendToClient(string clientId, string message)
    {
        if (!_clientsById.TryGetValue(clientId, out var connection))
        {
            return;
        }

        var isSent = await TryWriteAsync(clientId, connection, message);
        if (!isSent)
        {
            RemoveClient(clientId);
        }
    }

    private async Task<bool> TryWriteAsync(string clientId, SseClientConnection connection, string message)
    {
        await connection.WriteLock.WaitAsync();
        try
        {
            await connection.StreamWriter.WriteAsync($"data: {message}\n\n");
            await connection.StreamWriter.FlushAsync();
            return true;
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception, "Failed to send SSE message to client {ClientId}.", clientId);
            return false;
        }
        finally
        {
            connection.WriteLock.Release();
        }
    }

    private static string ToEventKey(AssoEventsId assoEventsId)
    {
        return assoEventsId.Value.ToString("D");
    }

    private sealed class SseClientConnection(string eventKey, StreamWriter streamWriter)
    {
        public string EventKey { get; } = eventKey;
        public StreamWriter StreamWriter { get; } = streamWriter;
        public SemaphoreSlim WriteLock { get; } = new(1, 1);
    }
}