namespace Vole_Papillon_Damour.Contracts.Books.Requests;

public sealed record AddAdminBookRequest(
    string Isbn13,
    int QuantityAvailable,
    string Note,
    string? Title = null,
    string? Authors = null,
    string? Publisher = null,
    int? PublicationYear = null,
    string? PhysicalFormat = null,
    string? Language = null,
    string? Genre = null,
    string? CoverBlobRef = null,
    string? WorkId = null,
    IReadOnlyCollection<string>? Fields = null);

public sealed record UpdateAdminBookMetadataRequest(
    string? Title,
    string? Authors,
    string? Publisher,
    int? PublicationYear,
    string? PhysicalFormat,
    string? Language,
    string? Genre,
    string? CoverBlobRef,
    string? WorkId,
    IReadOnlyCollection<string> Fields);

public sealed record CorrectBookQuantityRequest(int QuantityAvailable, string Note);

public sealed record WithdrawBookRequest(int Quantity, string Note);

public sealed record CorrectAnnouncementQuantityRequest(int Quantity, string Note);

public sealed record MergeBooksRequest(string TargetIsbn13, string Note);

public sealed record SetBookFairRevenueRequest(decimal? Revenue);

public sealed record ReassignAdminSessionRequest(string Mode, Guid? TargetAssoEventsId);

public sealed record UpdateAdminAssociationSettingsRequest(
    int DuplicateThreshold,
    int DemandSalesThreshold,
    int DeadStockMinAgeDays,
    int DeadStockMinQuantity,
    int WatchlistMaxItems,
    int AlertCooldownDays,
    int SessionIdleTimeoutMinutes,
    int AlertDelayMinutes);
