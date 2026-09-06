namespace Vole_Papillon_Damour.Contracts.Books.Responses;

public sealed record AdminStockSummaryResponse(
    int AvailableQuantity,
    int AvailableTitles,
    int AnnouncedQuantity,
    int AnnouncedTitles);

public sealed record AdminPeriodMetricsResponse(
    DateTimeOffset From,
    DateTimeOffset To,
    int ScannedCount,
    int KeptCount,
    int RejectedCount,
    int SoldQuantity,
    int SoldTitles);

public sealed record AdminFairSummaryResponse(
    Guid Id,
    string Name,
    DateTimeOffset DateStart,
    DateTimeOffset? DateEnd,
    int SoldQuantity,
    int SoldTitles,
    decimal? Revenue);

public sealed record AdminAlertQueueSummaryResponse(
    int PendingCount,
    DateTimeOffset? OldestDueAt,
    DateTimeOffset? NextDueAt);

public sealed record CatalogAdminOverviewResponse(
    DateTimeOffset GeneratedAt,
    AdminPeriodMetricsResponse CurrentPeriod,
    AdminPeriodMetricsResponse PreviousPeriod,
    AdminStockSummaryResponse Stock,
    AdminFairSummaryResponse? LastFair,
    int DeadStockCount,
    int RareQueueCount,
    int MetadataMissingCount,
    int UndatedAnnouncementCount,
    int InventoryDriftTitleCount,
    int InventoryDriftQuantity,
    AdminAlertQueueSummaryResponse PendingAlerts);

public sealed record AdminAnnouncementResponse(
    Guid Id,
    string Isbn13,
    Guid? FairId,
    int Quantity,
    string Status,
    DateTimeOffset CreatedAt,
    DateTimeOffset? ReleasedAt,
    Guid ScanSessionId);

public sealed record AdminBookMovementResponse(
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

public sealed record AdminBookResponse(
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
    IReadOnlyList<AdminAnnouncementResponse> Announcements,
    IReadOnlyList<AdminBookMovementResponse> Movements);

public sealed record AdminBookPageResponse(
    DateTimeOffset GeneratedAt,
    IReadOnlyList<AdminBookResponse> Books,
    int TotalCount,
    int Page,
    int PageSize);

public sealed record AdminBookOperationResponse(
    string Isbn13,
    int QuantityAvailable,
    int QuantityAnnounced,
    bool Changed,
    Guid? MovementId);

public sealed record AdminQuantityCorrectionResponse(
    string Isbn13,
    int PreviousQuantityAvailable,
    int QuantityAvailable,
    int Delta,
    bool Changed,
    Guid? MovementId);

public sealed record AdminFairResponse(
    Guid Id,
    string Name,
    DateTimeOffset DateStart,
    DateTimeOffset? DateEnd,
    bool IsCancelled,
    decimal? Revenue);

public sealed record AdminFairPageResponse(
    DateTimeOffset GeneratedAt,
    IReadOnlyList<AdminFairResponse> Fairs,
    int TotalCount,
    int Page,
    int PageSize);

public sealed record AdminGenreSalesResponse(string? Genre, int Quantity);

public sealed record AdminTopBookResponse(
    string Isbn13,
    string? Title,
    string? Authors,
    string? Genre,
    int Quantity);

public sealed record AdminDailySalesResponse(DateOnly Day, int Quantity);

public sealed record AdminFairComparisonResponse(
    Guid FairId,
    string Name,
    DateTimeOffset DateStart,
    int SoldQuantity,
    decimal? Revenue);

public sealed record AdminFairStatsResponse(
    AdminFairResponse Fair,
    int SoldQuantity,
    int SoldTitles,
    decimal? Revenue,
    decimal? AverageBasket,
    IReadOnlyList<AdminGenreSalesResponse> SalesByGenre,
    IReadOnlyList<AdminTopBookResponse> TopBooks,
    IReadOnlyList<AdminDailySalesResponse> DailySales,
    IReadOnlyList<AdminFairComparisonResponse> PreviousFairs);

public sealed record AdminScanSessionResponse(
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
    IReadOnlyList<AdminBookMovementResponse> Movements);

public sealed record AdminScanSessionPageResponse(
    DateTimeOffset GeneratedAt,
    IReadOnlyList<AdminScanSessionResponse> Sessions,
    int TotalCount,
    int Page,
    int PageSize);

public sealed record AdminScanSessionOperationResponse(
    Guid ScanSessionId,
    int AffectedMovementCount,
    int AffectedAlertCount,
    bool Changed);

public sealed record AdminAlertResponse(
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

public sealed record AdminAlertPageResponse(
    DateTimeOffset GeneratedAt,
    IReadOnlyList<AdminAlertResponse> Alerts,
    int TotalCount,
    int Page,
    int PageSize);

public sealed record AdminMemberSummaryResponse(
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

public sealed record AdminMemberWatchlistItemResponse(
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

public sealed record AdminMemberAlertHistoryResponse(
    Guid Id,
    string Isbn13,
    string? Title,
    DateTimeOffset SentAt,
    Guid? OutboxMessageId);

public sealed record AdminMemberDetailResponse(
    AdminMemberSummaryResponse Member,
    IReadOnlyList<AdminMemberWatchlistItemResponse> Watchlist,
    IReadOnlyList<AdminMemberAlertHistoryResponse> Alerts);

public sealed record AdminMemberPageResponse(
    DateTimeOffset GeneratedAt,
    IReadOnlyList<AdminMemberSummaryResponse> Members,
    int TotalCount,
    int Page,
    int PageSize);

public sealed record AdminMemberOperationResponse(
    Guid MemberId,
    string AlertStatus,
    bool Changed,
    bool DeletionCompleted);

public sealed record AdminAlertOperationResponse(
    Guid MessageId,
    string Status,
    bool Changed);

public sealed record AdminAssociationSettingsResponse(
    int DuplicateThreshold,
    int DemandSalesThreshold,
    int DeadStockMinAgeDays,
    int DeadStockMinQuantity,
    int WatchlistMaxItems,
    int AlertCooldownDays,
    int SessionIdleTimeoutMinutes,
    int AlertDelayMinutes,
    DateTimeOffset UpdatedAt,
    Guid UpdatedBy);

public sealed record BookReferenceSearchItemResponse(
    string? Isbn13,
    string? WorkId,
    string? Title,
    string? Authors,
    string? Publisher,
    int? PublicationYear,
    Uri? CoverUrl,
    string Source);

public sealed record BookReferenceSearchResponse(
    DateTimeOffset GeneratedAt,
    string Query,
    IReadOnlyList<BookReferenceSearchItemResponse> Items,
    int Page,
    int PageSize);
