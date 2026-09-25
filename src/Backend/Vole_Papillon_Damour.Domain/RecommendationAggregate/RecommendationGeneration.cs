using Vole_Papillon_Damour.Domain.Common;

namespace Vole_Papillon_Damour.Domain.RecommendationAggregate;

/// <summary>Ligne unique (Id = 1) : la génération de voisins que l'API lit.</summary>
public sealed class RecommendationGeneration
{
    public byte Id { get; private set; } = 1;
    public Guid? CurrentGenerationId { get; private set; }
    public DateTime? ComputedAt { get; private set; }
    public int BookCount { get; private set; }

    private RecommendationGeneration()
    {
    }

    public static RecommendationGeneration Singleton() => new();

    public void Switch(Guid generationId, int bookCount, DateTime computedAt)
    {
        CurrentGenerationId = generationId;
        BookCount = bookCount;
        ComputedAt = DomainTime.RequireUtc(computedAt, nameof(computedAt));
    }
}
