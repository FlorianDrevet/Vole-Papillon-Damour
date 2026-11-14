namespace Vole_Papillon_Damour.Application.Favorites;

public sealed class FavoritesOptions
{
    public const string SectionName = "Favorites";

    public int AccountMax { get; set; } = 200;
    public int LocalMax { get; set; } = 50;
    public int ReferencesPerVisitorPerDay { get; set; } = 20;
    public int ReferenceDailyIsbndbCredits { get; set; } = 300;
}
