namespace Vole_Papillon_Damour.Application.Common.Interfaces.Services;

public interface ISSEClientManager
{
    public void AddClient(string clientId, StreamWriter streamWriter);
    public void RemoveClient(string clientId);
    public Task SendToAllClients(string message);
    
    public Task SendToClient(string clientId, string message);
}