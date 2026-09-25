namespace Vole_Papillon_Damour.Infrastructure.Services.Recommendations;

public sealed class RecommendationOptions
{
    public const string SectionName = "Recommendations";

    public bool Enabled { get; set; }
    public string? Endpoint { get; set; }
    public string? EmbeddingDeploymentName { get; set; } = "text-embedding-3-small";
    public int Dimensions { get; set; } = 512;
    public int NeighborsPerBook { get; set; } = 30;
    public int ProfileBatchSize { get; set; } = 200;
    public float SimilarMinScore { get; set; } = 0.50f;
    public int SimilarMaxCount { get; set; } = 5;
    public int SimilarMinCount { get; set; } = 2;
    public int PersonalMaxCount { get; set; } = 8;
    public int PersonalMinCount { get; set; } = 3;
}
