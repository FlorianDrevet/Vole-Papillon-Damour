namespace Vole_Papillon_Damour.Domain.ManualRequestAggregate.ValueObjects;

public enum ManualBookRequestScope : byte
{
    Work = 0,
    Edition = 1
}

public enum ManualBookRequestStatus : byte
{
    Searching = 0,
    Found = 1,
    Given = 2,
    Refused = 3,
    Cancelled = 4
}

public enum ManualBookRequestFoundOrigin : byte
{
    Scan = 0,
    StockAvailable = 1,
    StockAnnounced = 2
}
