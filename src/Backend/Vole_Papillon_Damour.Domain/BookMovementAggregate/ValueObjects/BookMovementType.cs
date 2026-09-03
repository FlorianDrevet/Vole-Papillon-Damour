namespace Vole_Papillon_Damour.Domain.BookMovementAggregate.ValueObjects;

public enum BookMovementType : byte
{
    AnnouncementEntry,
    DirectEntry,
    FairRelease,
    Sale,
    Rejection,
    Correction,
    Withdrawal
}
