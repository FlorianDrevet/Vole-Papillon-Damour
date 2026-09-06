namespace Vole_Papillon_Damour.Application.Common.Models;

public enum BookAlertQueueStatus : byte
{
    Pending,
    Sent,
    Cancelled,
    Failed
}

public sealed record BookAlertOutboxAdminItem(
    Guid Id,
    Guid? ScanSessionId,
    Guid? MemberId,
    BookAlertQueueStatus Status,
    int ItemCount,
    int Attempts,
    DateTime CreatedAt,
    DateTime DueAt,
    DateTime? SentAt,
    string? LastError);

public sealed record BookAlertOutboxAdminPage(
    IReadOnlyList<BookAlertOutboxAdminItem> Items,
    int TotalCount,
    int Page,
    int PageSize);
