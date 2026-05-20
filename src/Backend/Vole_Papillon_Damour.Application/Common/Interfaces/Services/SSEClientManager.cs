using Vole_Papillon_Damour.Domain.EventsAggregate.ValueObjects;

namespace Vole_Papillon_Damour.Application.Common.Interfaces.Services;

public interface ISSEClientManager
{
    public void AddClient(AssoEventsId assoEventsId, string clientId, StreamWriter streamWriter);
    public void RemoveClient(string clientId);
    public Task SendToEvent(AssoEventsId assoEventsId, string message);
    public Task SendToClient(string clientId, string message);
}