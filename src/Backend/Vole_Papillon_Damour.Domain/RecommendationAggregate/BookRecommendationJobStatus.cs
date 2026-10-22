namespace Vole_Papillon_Damour.Domain.RecommendationAggregate;

public enum BookRecommendationJobStatus : byte
{
    Pending = 0,
    InProgress = 1,
    Ready = 2,
    Failed = 3,
    NoNotice = 4,
}
