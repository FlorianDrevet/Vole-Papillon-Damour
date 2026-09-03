namespace Vole_Papillon_Damour.Domain.ScanSessionAggregate.ValueObjects;

public enum ScanCloseReason : byte
{
    Manual,
    Inactivity,
    Disconnect,
    TokenExpired
}
