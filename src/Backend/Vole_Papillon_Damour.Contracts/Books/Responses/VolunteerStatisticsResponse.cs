namespace Vole_Papillon_Damour.Contracts.Books.Responses;

public sealed record VolunteerStatisticsResponse(
    DateTimeOffset GeneratedAt,
    DateTimeOffset? MemberSince,
    VolunteerScanStatisticsResponse Scan,
    VolunteerCashStatisticsResponse Cash);

public sealed record VolunteerScanStatisticsResponse(
    int ScannedCount,
    int KeptCount,
    int RejectedCount,
    int SessionCount,
    int DurationMinutes,
    DateTimeOffset? FirstSessionAt,
    decimal? MedianTeamKeepRatePercent,
    IReadOnlyList<VolunteerMonthlyStatisticsResponse> Monthly,
    IReadOnlyList<VolunteerTimeSlotStatisticsResponse> TimeSlots,
    VolunteerImpactStatisticsResponse Impact,
    IReadOnlyList<VolunteerGenreStatisticsResponse> TopGenres,
    IReadOnlyList<VolunteerSessionStatisticsResponse> RecentSessions);

public sealed record VolunteerCashStatisticsResponse(
    int GrossSoldQuantity,
    int SoldQuantity,
    int SaleMovementCount,
    int VoidedSaleQuantity,
    int FairCount,
    int EstimatedDurationMinutes,
    int? EstimatedCadencePerHour,
    int? MedianTeamCadencePerHour,
    IReadOnlyList<VolunteerFairStatisticsResponse> FairBreakdown,
    VolunteerPeakStatisticsResponse Peak,
    IReadOnlyList<VolunteerBookStatisticsResponse> TopBooks,
    IReadOnlyList<VolunteerGenreStatisticsResponse> TopGenres,
    int EstimatedTriAndCashOverlap,
    decimal? EstimatedRevenueShare,
    VolunteerRevenueShareResponse? RevenueShare,
    bool DurationIsEstimated,
    bool CadenceIsEstimated,
    bool RevenueShareIsEstimated,
    bool TriAndCashOverlapIsEstimated);

public sealed record VolunteerMonthlyStatisticsResponse(
    DateTimeOffset PeriodStart,
    int Kept,
    int Rejected);

public sealed record VolunteerTimeSlotStatisticsResponse(
    int DayOfWeek,
    string Slot,
    int Count);

public sealed record VolunteerImpactStatisticsResponse(
    int FoundReaderCount,
    int NewTitleCount,
    int RareCount,
    int AlertItemCount,
    bool FoundReaderIsEstimated);

public sealed record VolunteerGenreStatisticsResponse(
    string Name,
    int Quantity);

public sealed record VolunteerSessionStatisticsResponse(
    Guid Id,
    DateTimeOffset StartedAt,
    int DurationMinutes,
    int ScannedCount,
    decimal KeptRatePercent,
    string Mode);

public sealed record VolunteerFairStatisticsResponse(
    Guid Id,
    string Name,
    DateTimeOffset DateStart,
    int NetSoldQuantity);

public sealed record VolunteerPeakStatisticsResponse(
    DateTimeOffset? FairDate,
    int? LocalHour,
    int Quantity);

public sealed record VolunteerBookStatisticsResponse(
    string Isbn13,
    string Title,
    int Quantity);

public sealed record VolunteerRevenueShareResponse(
    Guid FairId,
    string FairName,
    DateTimeOffset FairDate,
    decimal FairRevenue,
    int VolunteerSoldQuantity,
    int FairSoldQuantity,
    decimal EstimatedShare);
