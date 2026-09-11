using Vole_Papillon_Damour.Domain.ActualityAggregate.ValueObjects;
using Vole_Papillon_Damour.Domain.Common.Models;

namespace Vole_Papillon_Damour.Domain.ActualityAggregate;

public sealed class SocialPostImport : AggregateRoot<Guid>
{
    public SocialPostSource Source { get; private set; }
    public string ExternalId { get; private set; } = null!;
    public Uri Permalink { get; private set; } = null!;
    public DateTimeOffset PublishedAt { get; private set; }
    public DateTimeOffset ImportedAt { get; private set; }
    public ActualityId? ActualityId { get; private set; }

    private SocialPostImport(
        Guid id,
        SocialPostSource source,
        string externalId,
        Uri permalink,
        DateTimeOffset publishedAt,
        DateTimeOffset importedAt,
        ActualityId? actualityId) : base(id)
    {
        Source = source;
        ExternalId = externalId.Trim();
        Permalink = permalink;
        PublishedAt = publishedAt.ToUniversalTime();
        ImportedAt = importedAt.ToUniversalTime();
        ActualityId = actualityId;
    }

    public static SocialPostImport Create(
        Guid id,
        SocialPostSource source,
        string externalId,
        Uri permalink,
        DateTimeOffset publishedAt,
        DateTimeOffset importedAt,
        ActualityId? actualityId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(externalId);
        ArgumentNullException.ThrowIfNull(permalink);

        return new SocialPostImport(
            id,
            source,
            externalId,
            permalink,
            publishedAt,
            importedAt,
            actualityId);
    }

    public void DetachActuality()
    {
        ActualityId = null;
    }

    private SocialPostImport()
    {
    }
}
