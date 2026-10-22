using MediatR;

namespace Vole_Papillon_Damour.Application.Recommendations.Commands.RefreshBookSimilarityProfiles;

public sealed record RefreshBookSimilarityProfilesCommand(string? Isbn13 = null)
    : IRequest<RefreshBookSimilarityProfilesResult>;

public sealed record RefreshBookSimilarityProfilesResult(int Candidates, int Found, int NotFound);
