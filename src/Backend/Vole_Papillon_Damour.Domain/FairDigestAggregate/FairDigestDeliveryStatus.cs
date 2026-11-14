namespace Vole_Papillon_Damour.Domain.FairDigestAggregate;

public enum FairDigestDeliveryStatus : byte
{
    Pending,
    Sent,
    SkippedNoContent,
    Failed,
}
