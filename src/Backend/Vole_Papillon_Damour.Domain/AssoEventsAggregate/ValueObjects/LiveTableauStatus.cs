namespace Vole_Papillon_Damour.Domain.AssoEventsAggregate.ValueObjects;

/// <summary>
/// Where a loto stands for its public audience: before the first number,
/// while numbers are drawn, or after the last partie has been won.
/// </summary>
public enum LiveTableauStatus
{
    NotStarted,
    Live,
    Finished
}
