namespace Vole_Papillon_Damour.Domain.RecommendationAggregate;

public enum BookRecommendationJobStage : byte
{
    Notice = 0,
    Embedding = 1,
    Neighbors = 2,
}
