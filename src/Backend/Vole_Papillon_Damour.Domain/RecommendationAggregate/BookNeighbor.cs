using Vole_Papillon_Damour.Domain.RecommendationAggregate.ValueObjects;

namespace Vole_Papillon_Damour.Domain.RecommendationAggregate;

/// <summary>Un voisin d'un livre dans une génération de calcul. Modèle de lecture, écrit en masse.</summary>
public sealed class BookNeighbor
{
    public Guid GenerationId { get; private set; }
    public string Isbn13 { get; private set; } = string.Empty;
    public byte Rank { get; private set; }
    public string NeighborIsbn13 { get; private set; } = string.Empty;
    public float Score { get; private set; }
    public NeighborReason Reason { get; private set; }

    private BookNeighbor()
    {
    }

    public BookNeighbor(Guid generationId, string isbn13, byte rank, string neighborIsbn13, float score, NeighborReason reason)
    {
        GenerationId = generationId;
        Isbn13 = isbn13;
        Rank = rank;
        NeighborIsbn13 = neighborIsbn13;
        Score = score;
        Reason = reason;
    }
}
