namespace Vole_Papillon_Damour.Application.Isbndb;

public sealed class IsbndbPublicSearchOptions
{
    public int SearchCacheMinutes { get; set; } = 60;
    public int PublicSearchDailyCredits { get; set; } = 300;
}
