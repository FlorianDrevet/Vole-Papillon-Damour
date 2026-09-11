namespace Vole_Papillon_Damour.Application.Books.Common;

public sealed record VolunteerStatisticsResult(
    DateTimeOffset GeneratedAt,
    DateTimeOffset? MemberSince,
    VolunteerScanStatisticsResult Scan,
    VolunteerCashStatisticsResult Cash);

public sealed record VolunteerScanStatisticsResult(
    int ScannedCount,
    int KeptCount,
    int RejectedCount,
    int SessionCount,
    int DurationMinutes,
    DateTimeOffset? FirstSessionAt,
    decimal? MedianTeamKeepRatePercent,
    IReadOnlyList<VolunteerMonthlyStatisticsResult> Monthly,
    IReadOnlyList<VolunteerTimeSlotStatisticsResult> TimeSlots,
    VolunteerImpactStatisticsResult Impact,
    IReadOnlyList<VolunteerGenreStatisticsResult> TopGenres,
    IReadOnlyList<VolunteerSessionStatisticsResult> RecentSessions);

public sealed record VolunteerCashStatisticsResult(
    int GrossSoldQuantity,
    int SoldQuantity,
    int SaleMovementCount,
    int VoidedSaleQuantity,
    int FairCount,
    int EstimatedDurationMinutes,
    int? EstimatedCadencePerHour,
    int? MedianTeamCadencePerHour,
    IReadOnlyList<VolunteerFairStatisticsResult> FairBreakdown,
    VolunteerPeakStatisticsResult Peak,
    IReadOnlyList<VolunteerBookStatisticsResult> TopBooks,
    IReadOnlyList<VolunteerGenreStatisticsResult> TopGenres,
    int EstimatedTriAndCashOverlap,
    decimal? EstimatedRevenueShare,
    VolunteerRevenueShareResult? RevenueShare,
    bool DurationIsEstimated,
    bool CadenceIsEstimated,
    bool RevenueShareIsEstimated,
    bool TriAndCashOverlapIsEstimated);

public sealed record VolunteerMonthlyStatisticsResult(
    DateTimeOffset PeriodStart,
    int Kept,
    int Rejected);

public sealed record VolunteerTimeSlotStatisticsResult(
    int DayOfWeek,
    string Slot,
    int Count);

public sealed record VolunteerImpactStatisticsResult(
    int FoundReaderCount,
    int NewTitleCount,
    int RareCount,
    int AlertItemCount,
    bool FoundReaderIsEstimated);

public sealed record VolunteerGenreStatisticsResult(
    string Name,
    int Quantity);

public sealed record VolunteerSessionStatisticsResult(
    Guid Id,
    DateTimeOffset StartedAt,
    int DurationMinutes,
    int ScannedCount,
    decimal KeptRatePercent,
    string Mode);

public sealed record VolunteerFairStatisticsResult(
    Guid Id,
    string Name,
    DateTimeOffset DateStart,
    int NetSoldQuantity);

public sealed record VolunteerPeakStatisticsResult(
    DateTimeOffset? FairDate,
    int? LocalHour,
    int Quantity);

public sealed record VolunteerBookStatisticsResult(
    string Isbn13,
    string Title,
    int Quantity);

public sealed record VolunteerRevenueShareResult(
    Guid FairId,
    string FairName,
    DateTimeOffset FairDate,
    decimal FairRevenue,
    int VolunteerSoldQuantity,
    int FairSoldQuantity,
    decimal EstimatedShare);
