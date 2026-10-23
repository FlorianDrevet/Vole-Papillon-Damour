namespace Vole_Papillon_Damour.Infrastructure.Services.Isbndb;

public sealed record IsbndbOptions
{
    public const string SectionName = "Isbndb";

    public string ApiKey { get; init; } = "";
    public string BaseUrl { get; init; } = "https://api2.isbndb.com";
    public int RequestsPerSecond { get; init; } = 1;
    public int BulkSize { get; init; } = 100;
    public int TimeoutMilliseconds { get; init; } = 10_000;
    public int MaxRetries { get; init; } = 3;
    public int LookupCacheHours { get; init; } = 24;
    public int SearchCacheMinutes { get; init; } = 60;
    public int PublicSearchPerVisitorPerHour { get; init; } = 10;
    public int PublicSearchDailyCredits { get; init; } = 300;
    public int AbsentRecheckDays { get; init; } = 30;
    public string CatchUpSchedule { get; init; } = "0 30 2 * * *";
}
