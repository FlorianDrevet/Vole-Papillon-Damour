namespace Vole_Papillon_Damour.Application.FairDigest.Common;

public sealed class FairDigestOptions
{
    public const string SectionName = "FairDigest";

    public bool Enabled { get; set; }
    public int DaysBefore { get; set; } = 3;
    public int SendHourParis { get; set; } = 10;
    public string Schedule { get; set; } = "0 5 * * * *";
    public int NewsMax { get; set; } = 8;
    public int BackMax { get; set; } = 6;
    public int BackMinCount { get; set; } = 3;
    public int SoldOutMinSales { get; set; } = 3;
    public int TopSellersCount { get; set; } = 20;
}
