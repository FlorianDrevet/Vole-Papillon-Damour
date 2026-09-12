namespace Vole_Papillon_Damour.Application.Books.Common;

public sealed record AdminVolunteerStatisticsResult(
    DateTimeOffset GeneratedAt,
    DateTimeOffset? From,
    DateTimeOffset? To,
    Guid? FairId,
    AdminVolunteerTeamSummaryResult Team,
    IReadOnlyList<AdminVolunteerContributionResult> Volunteers,
    IReadOnlyList<AdminVolunteerMonthlyActivityResult> MonthlyActivity,
    AdminVolunteerRenewalResult Renewal,
    IReadOnlyList<AdminVolunteerGenreResult> DominantGenres);

public sealed record AdminVolunteerTeamSummaryResult(
    int ActiveVolunteerCount,
    int ScannedCount,
    int KeptCount,
    int RejectedCount,
    decimal? KeptRatePercent,
    int SoldQuantity,
    decimal? SoldOfKeptRatePercent,
    int SessionCount,
    int ScanDurationMinutes,
    int CashDurationMinutes,
    int TotalDurationMinutes,
    decimal? AverageSessionsPerVolunteer);

public sealed record AdminVolunteerContributionResult(
    Guid VolunteerId,
    string DisplayName,
    IReadOnlyList<string> Roles,
    int SessionCount,
    int ScanDurationMinutes,
    int CashDurationMinutes,
    int TotalDurationMinutes,
    int ScannedCount,
    int KeptCount,
    decimal? KeptRatePercent,
    int SoldQuantity,
    int WaitingQuantity,
    int WaitingOverYearQuantity,
    decimal? FlowRatePercent,
    DateTimeOffset? FirstActivityAt,
    DateTimeOffset? LastActivityAt,
    string? DominantGenre);

public sealed record AdminVolunteerMonthlyActivityResult(
    Guid VolunteerId,
    string DisplayName,
    IReadOnlyList<AdminVolunteerMonthResult> Months);

public sealed record AdminVolunteerMonthResult(
    DateTimeOffset PeriodStart,
    int SessionCount);

public sealed record AdminVolunteerRenewalResult(
    int NewCount,
    int RegularCount,
    int WithdrawingCount,
    int WindowDays);

public sealed record AdminVolunteerGenreResult(string Name, int Quantity);
