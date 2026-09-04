using Vole_Papillon_Damour.Domain.BookAggregate.ValueObjects;
using Vole_Papillon_Damour.Domain.ScanSessionAggregate.ValueObjects;

namespace Vole_Papillon_Damour.Application.Common.Models;

public sealed record BookAlertOutboxItem(
    Isbn13 Isbn13,
    string? WorkId,
    string? Title,
    string? Authors,
    int Quantity,
    ScanMode Mode,
    Guid? AssoEventsId,
    string? Publisher = null,
    int? PublicationYear = null,
    string? PhysicalFormat = null,
    DateTimeOffset? FairOpeningAt = null);

public sealed record BookAlertOutboxWorkItem(
    Guid MessageId,
    Guid MemberId,
    IReadOnlyList<BookAlertOutboxItem> Items,
    int Attempts,
    DateTime ClaimedUntil);

public sealed record BookAlertDelivery(
    Guid MessageId,
    Guid MemberId,
    string Email,
    string? RecipientName,
    IReadOnlyList<BookAlertOutboxItem> Items);
