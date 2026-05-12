namespace Vole_Papillon_Damour.Contracts.Events.Requests.Parties;

public class UpdatePartieRequest
{
    public string Name { get; init; } = string.Empty;
    public string PartieType { get; init; } = string.Empty;
    public bool PauseAfter { get; init; }
}