namespace Vole_Papillon_Damour.Domain.IsbndbAggregate;

public enum IsbndbCatchUpStopReason : byte
{
    Completed,
    QuotaExhausted,
    Unavailable,
    Cancelled
}
