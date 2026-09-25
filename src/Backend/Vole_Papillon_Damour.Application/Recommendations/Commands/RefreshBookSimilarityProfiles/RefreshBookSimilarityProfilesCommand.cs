using MediatR;

namespace Vole_Papillon_Damour.Application.Recommendations.Commands.RefreshBookSimilarityProfiles;

public sealed record RefreshBookSimilarityProfilesCommand : IRequest<RefreshBookSimilarityProfilesResult>;

public sealed record RefreshBookSimilarityProfilesResult(int Candidates, int Found, int NotFound);
