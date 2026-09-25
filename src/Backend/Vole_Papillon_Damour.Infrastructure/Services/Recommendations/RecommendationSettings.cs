using Microsoft.Extensions.Options;
using Vole_Papillon_Damour.Application.Common.Interfaces.Services;

namespace Vole_Papillon_Damour.Infrastructure.Services.Recommendations;

public sealed class RecommendationSettings(IOptions<RecommendationOptions> options) : IRecommendationSettings
{
    private RecommendationOptions Values => options.Value;

    public bool Enabled => Values.Enabled;
    public int NeighborsPerBook => Values.NeighborsPerBook;
    public int ProfileBatchSize => Values.ProfileBatchSize;
    public float SimilarMinScore => Values.SimilarMinScore;
    public int SimilarMaxCount => Values.SimilarMaxCount;
    public int SimilarMinCount => Values.SimilarMinCount;
    public int PersonalMaxCount => Values.PersonalMaxCount;
    public int PersonalMinCount => Values.PersonalMinCount;
}
