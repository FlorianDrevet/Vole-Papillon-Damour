namespace Vole_Papillon_Damour.Contracts.Events.Requests;

public class RemoveLastNumeroRequest
{
    public Guid? AssoEventId { get; set; }
    public Guid? PartieId { get; set; }
}