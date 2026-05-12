using System.Collections.Concurrent;
using System.Text.Json;
using Vole_Papillon_Damour.Application.Common.Interfaces.Services;
using Vole_Papillon_Damour.Application.Events.Common;
using Vole_Papillon_Damour.Domain.AssoEventsAggregate;

namespace Vole_Papillon_Damour.Infrastructure.Services;

public class SSEClientManager: ISSEClientManager
{
    public ConcurrentDictionary<string, StreamWriter> Clients { get; } = new ConcurrentDictionary<string, StreamWriter>();

    public void AddClient(string clientId, StreamWriter streamWriter)
    {
        Clients.TryAdd(clientId, streamWriter);
    }

    public void RemoveClient(string clientId)
    {
        Clients.TryRemove(clientId, out _);
    }

    public async Task SendToAllClients(string message)
    {
        foreach (var client in Clients.Values)
        {
            await client.WriteLineAsync($"data: {message}\n");
            await client.FlushAsync();
        }
    }

    public async Task SendToClient(string clientId, string message)
    {
        if (Clients.TryGetValue(clientId, out var client))
        {
            await client.WriteLineAsync($"data: {message}\n");
            await client.FlushAsync();
        }
    }
}