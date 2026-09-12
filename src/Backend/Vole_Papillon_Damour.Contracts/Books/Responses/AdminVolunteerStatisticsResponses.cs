namespace Vole_Papillon_Damour.Contracts.Books.Responses;

public sealed record AdminVolunteerStatisticsResponse(
    DateTimeOffset GeneratedAt,
    DateTimeOffset? From,
    DateTimeOffset? To,
    Guid? FairId,
    AdminVolunteerTeamSummaryResponse Team,
    IReadOnlyList<AdminVolunteerContributionResponse> Volunteers,
    IReadOnlyList<AdminVolunteerMonthlyActivityResponse> MonthlyActivity,
    AdminVolunteerRenewalResponse Renewal,
    IReadOnlyList<AdminVolunteerGenreResponse> DominantGenres);

public sealed record AdminVolunteerTeamSummaryResponse(
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

public sealed record AdminVolunteerContributionResponse(
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

public sealed record AdminVolunteerMonthlyActivityResponse(
    Guid VolunteerId,
    string DisplayName,
    IReadOnlyList<AdminVolunteerMonthResponse> Months);

public sealed record AdminVolunteerMonthResponse(
    DateTimeOffset PeriodStart,
    int SessionCount);

public sealed record AdminVolunteerRenewalResponse(
    int NewCount,
    int RegularCount,
    int WithdrawingCount,
    int WindowDays);

public sealed record AdminVolunteerGenreResponse(string Name, int Quantity);
