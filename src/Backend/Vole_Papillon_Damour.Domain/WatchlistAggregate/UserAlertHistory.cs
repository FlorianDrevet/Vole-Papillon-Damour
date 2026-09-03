using Vole_Papillon_Damour.Domain.BookAggregate.ValueObjects;
using Vole_Papillon_Damour.Domain.Common;
using Vole_Papillon_Damour.Domain.Common.Models;
using Vole_Papillon_Damour.Domain.UserAggregate.ValueObjects;

namespace Vole_Papillon_Damour.Domain.WatchlistAggregate;

public sealed class UserAlertHistory : Entity<Guid>
{
    public UserId UserId { get; private set; } = null!;
    public Isbn13 Isbn13 { get; private set; }
    public DateTime SentAt { get; private set; }
    public Guid? OutboxMessageId { get; private set; }

    private UserAlertHistory(
        Guid id,
        UserId userId,
        Isbn13 isbn13,
        DateTime sentAt,
        Guid? outboxMessageId) : base(id)
    {
        if (id == Guid.Empty)
        {
            throw new ArgumentException("An alert history identifier is required.", nameof(id));
        }

        if (userId is null || userId.Value == Guid.Empty)
        {
            throw new ArgumentException("A valid user identifier is required.", nameof(userId));
        }

        if (string.IsNullOrWhiteSpace(isbn13.Value))
        {
            throw new ArgumentException("A valid ISBN-13 is required.", nameof(isbn13));
        }

        if (outboxMessageId == Guid.Empty)
        {
            throw new ArgumentException("An outbox identifier must be null or non-empty.", nameof(outboxMessageId));
        }

        UserId = userId;
        Isbn13 = isbn13;
        SentAt = DomainTime.RequireUtc(sentAt, nameof(sentAt));
        OutboxMessageId = outboxMessageId;
    }

    public static UserAlertHistory Create(
        Guid id,
        UserId userId,
        Isbn13 isbn13,
        DateTime sentAt,
        Guid? outboxMessageId = null)
    {
        return new UserAlertHistory(id, userId, isbn13, sentAt, outboxMessageId);
    }

    public UserAlertHistory()
    {
    }
}
