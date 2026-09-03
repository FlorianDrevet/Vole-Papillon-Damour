using Vole_Papillon_Damour.Domain.Common;
using Vole_Papillon_Damour.Domain.Common.Models;
using Vole_Papillon_Damour.Domain.UserAggregate.ValueObjects;

namespace Vole_Papillon_Damour.Domain.WatchlistAggregate;

public sealed class EmailBounceEvent : Entity<Guid>
{
    public const int MaxProviderEventIdLength = 128;

    public string ProviderEventId { get; private set; } = null!;
    public UserId UserId { get; private set; } = null!;
    public DateTime RecordedAt { get; private set; }

    private EmailBounceEvent(
        Guid id,
        string providerEventId,
        UserId userId,
        DateTime recordedAt) : base(id)
    {
        if (id == Guid.Empty)
        {
            throw new ArgumentException("An email bounce event identifier is required.", nameof(id));
        }

        if (string.IsNullOrWhiteSpace(providerEventId))
        {
            throw new ArgumentException("A provider event identifier is required.", nameof(providerEventId));
        }

        var normalizedProviderEventId = providerEventId.Trim();
        if (normalizedProviderEventId.Length > MaxProviderEventIdLength)
        {
            throw new ArgumentOutOfRangeException(
                nameof(providerEventId),
                $"The provider event identifier cannot exceed {MaxProviderEventIdLength} characters.");
        }

        if (userId is null || userId.Value == Guid.Empty)
        {
            throw new ArgumentException("A valid user identifier is required.", nameof(userId));
        }

        ProviderEventId = normalizedProviderEventId;
        UserId = userId;
        RecordedAt = DomainTime.RequireUtc(recordedAt, nameof(recordedAt));
    }

    public static EmailBounceEvent Create(
        Guid id,
        string providerEventId,
        UserId userId,
        DateTime recordedAt)
    {
        return new EmailBounceEvent(id, providerEventId, userId, recordedAt);
    }

    public EmailBounceEvent()
    {
    }
}
