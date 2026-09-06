using Vole_Papillon_Damour.Domain.BookAggregate.ValueObjects;
using Vole_Papillon_Damour.Domain.BookMovementAggregate.ValueObjects;
using Vole_Papillon_Damour.Domain.EventsAggregate.ValueObjects;
using Vole_Papillon_Damour.Domain.ScanSessionAggregate.ValueObjects;
using Vole_Papillon_Damour.Domain.WatchlistAggregate.ValueObjects;

namespace Vole_Papillon_Damour.Application.Books.Common;

public sealed record AdminStockSummaryResult(
    int AvailableQuantity,
    int AvailableTitles,
    int AnnouncedQuantity,
    int AnnouncedTitles);

public sealed record AdminPeriodMetricsResult(
    DateTimeOffset From,
    DateTimeOffset To,
    int ScannedCount,
    int KeptCount,
    int RejectedCount,
    int SoldQuantity,
    int SoldTitles);

public sealed record AdminFairSummaryResult(
    Guid Id,
    string Name,
    DateTimeOffset DateStart,
    DateTimeOffset? DateEnd,
    int SoldQuantity,
    int SoldTitles,
    decimal? Revenue);

public sealed record AdminAlertQueueSummaryResult(
    int PendingCount,
    DateTimeOffset? OldestDueAt,
    DateTimeOffset? NextDueAt);

public sealed record CatalogAdminOverviewResult(
    DateTimeOffset GeneratedAt,
    AdminPeriodMetricsResult CurrentPeriod,
    AdminPeriodMetricsResult PreviousPeriod,
    AdminStockSummaryResult Stock,
    AdminFairSummaryResult? LastFair,
    int DeadStockCount,
    int RareQueueCount,
    int MetadataMissingCount,
    int UndatedAnnouncementCount,
    int InventoryDriftTitleCount,
    int InventoryDriftQuantity,
    AdminAlertQueueSummaryResult PendingAlerts);

public sealed record AdminBookPageResult(
    DateTimeOffset GeneratedAt,
    IReadOnlyList<AdminBookResult> Books,
    int TotalCount,
    int Page,
    int PageSize);

public sealed record AdminBookResult(
    string Isbn13,
    string? WorkId,
    string? Title,
    string? Authors,
    string? Publisher,
    int? PublicationYear,
    string? PhysicalFormat,
    string? Language,
    string? Genre,
    string MetadataStatus,
    string? MetadataSource,
    string? ManuallyEditedFields,
    int QuantityAvailable,
    int QuantityAnnounced,
    int SalesCount,
    int RejectionCount,
    bool IsRare,
    bool IsHidden,
    string? RedirectedToIsbn13,
    string? CoverUrl,
    DateTimeOffset FirstSeenAt,
    DateTimeOffset? LastAvailableAt,
    DateTimeOffset UpdatedAt,
    IReadOnlyList<AdminAnnouncementResult> Announcements,
    IReadOnlyList<AdminBookMovementResult> Movements);

public sealed record AdminAnnouncementResult(
    Guid Id,
    string Isbn13,
    Guid? FairId,
    int Quantity,
    string Status,
    DateTimeOffset CreatedAt,
    DateTimeOffset? ReleasedAt,
    Guid ScanSessionId);

public sealed record AdminBookMovementResult(
    Guid Id,
    string Isbn13,
    string Type,
    int Quantity,
    DateTimeOffset OccurredAt,
    DateTimeOffset ReceivedAt,
    bool ClockSuspect,
    Guid? ScanSessionId,
    Guid? VolunteerId,
    Guid? FairId,
    string? Note,
    Guid? ClientGestureId,
    Guid? ReversalOfMovementId);

public sealed record AdminBookOperationResult(
    string Isbn13,
    int QuantityAvailable,
    int QuantityAnnounced,
    bool Changed,
    Guid? MovementId = null);

public sealed record AdminFairResult(
    Guid Id,
    string Name,
    DateTimeOffset DateStart,
    DateTimeOffset? DateEnd,
    bool IsCancelled,
    decimal? Revenue);

public sealed record AdminFairStatsResult(
    AdminFairResult Fair,
    int SoldQuantity,
    int SoldTitles,
    decimal? Revenue,
    decimal? AverageBasket,
    IReadOnlyList<AdminGenreSalesResult> SalesByGenre,
    IReadOnlyList<AdminTopBookResult> TopBooks,
    IReadOnlyList<AdminDailySalesResult> DailySales,
    IReadOnlyList<AdminFairComparisonResult> PreviousFairs);

public sealed record AdminGenreSalesResult(string? Genre, int Quantity);

public sealed record AdminTopBookResult(
    string Isbn13,
    string? Title,
    string? Authors,
    string? Genre,
    int Quantity);

public sealed record AdminDailySalesResult(DateOnly Day, int Quantity);

public sealed record AdminFairComparisonResult(
    Guid FairId,
    string Name,
    DateTimeOffset DateStart,
    int SoldQuantity,
    decimal? Revenue);

public sealed record AdminFairPageResult(
    DateTimeOffset GeneratedAt,
    IReadOnlyList<AdminFairResult> Fairs,
    int TotalCount,
    int Page,
    int PageSize);

public sealed record AdminScanSessionPageResult(
    DateTimeOffset GeneratedAt,
    IReadOnlyList<AdminScanSessionResult> Sessions,
    int TotalCount,
    int Page,
    int PageSize);

public sealed record AdminScanSessionResult(
    Guid Id,
    Guid VolunteerId,
    string? VolunteerName,
    string Mode,
    Guid? FairId,
    string? FairName,
    DateTimeOffset StartedAt,
    DateTimeOffset LastScanAt,
    DateTimeOffset LastSyncAt,
    DateTimeOffset? EndedAt,
    string? CloseReason,
    string Status,
    int ScannedCount,
    int KeptCount,
    int RejectedCount,
    int AlertCount,
    int PendingAlertCount,
    DateTimeOffset? NextAlertDueAt,
    IReadOnlyList<AdminBookMovementResult> Movements);

public sealed record AdminScanSessionOperationResult(
    Guid ScanSessionId,
    int AffectedMovementCount,
    int AffectedAlertCount,
    bool Changed);

public sealed record AdminAlertPageResult(
    DateTimeOffset GeneratedAt,
    IReadOnlyList<AdminAlertResult> Alerts,
    int TotalCount,
    int Page,
    int PageSize);

public sealed record AdminAlertResult(
    Guid Id,
    Guid? ScanSessionId,
    Guid? MemberId,
    string Status,
    int ItemCount,
    int Attempts,
    DateTimeOffset CreatedAt,
    DateTimeOffset DueAt,
    DateTimeOffset? SentAt,
    string? LastError);

public sealed record AdminMemberPageResult(
    DateTimeOffset GeneratedAt,
    IReadOnlyList<AdminMemberSummaryResult> Members,
    int TotalCount,
    int Page,
    int PageSize);

public sealed record AdminMemberSummaryResult(
    Guid Id,
    string? ExternalId,
    string? Email,
    string? DisplayName,
    DateTimeOffset CreatedAt,
    DateTimeOffset LastSeenAt,
    DateTimeOffset? AnonymizedAt,
    string AlertStatus,
    int BounceCount,
    int WatchlistItemCount,
    int AlertHistoryCount);

public sealed record AdminMemberDetailResult(
    AdminMemberSummaryResult Member,
    IReadOnlyList<AdminMemberWatchlistItemResult> Watchlist,
    IReadOnlyList<AdminMemberAlertHistoryResult> Alerts);

public sealed record AdminMemberWatchlistItemResult(
    Guid Id,
    string Scope,
    string? WorkId,
    string? Isbn13,
    string? Title,
    string? Authors,
    int QuantityAvailable,
    int QuantityAnnounced,
    DateTimeOffset AddedAt,
    DateTimeOffset? LastAlertAt);

public sealed record AdminMemberAlertHistoryResult(
    Guid Id,
    string Isbn13,
    string? Title,
    DateTimeOffset SentAt,
    Guid? OutboxMessageId);

public sealed record AdminMemberOperationResult(
    Guid MemberId,
    string AlertStatus,
    bool Changed,
    bool DeletionCompleted = false);

public sealed record AdminAlertOperationResult(
    Guid MessageId,
    string Status,
    bool Changed);

public sealed record BookReferenceSearchResult(
    DateTimeOffset GeneratedAt,
    string Query,
    IReadOnlyList<BookReferenceSearchItem> Items,
    int Page,
    int PageSize);

public sealed record BookReferenceSearchItem(
    string? Isbn13,
    string? WorkId,
    string? Title,
    string? Authors,
    string? Publisher,
    int? PublicationYear,
    Uri? CoverUrl,
    string Source);
