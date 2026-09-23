namespace Vole_Papillon_Damour.Domain.CheckoutPassageAggregate.ValueObjects;

public enum CheckoutPassageStatus : byte
{
    PendingAssociation,
    Associated,
    Unresolved,
    Dissociated,
}
