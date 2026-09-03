namespace Vole_Papillon_Damour.Domain.BookAggregate.ValueObjects;

public enum BookMetadataStatus : byte
{
    Pending,
    Resolved,
    NotFound,
    Manual
}
