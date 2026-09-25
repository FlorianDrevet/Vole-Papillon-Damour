namespace Vole_Papillon_Damour.Application.Common.Interfaces.Services;

public interface IRecommendationSettings
{
    bool Enabled { get; }
    int NeighborsPerBook { get; }
    int ProfileBatchSize { get; }
    float SimilarMinScore { get; }
    int SimilarMaxCount { get; }
    int SimilarMinCount { get; }
    int PersonalMaxCount { get; }
    int PersonalMinCount { get; }
}
