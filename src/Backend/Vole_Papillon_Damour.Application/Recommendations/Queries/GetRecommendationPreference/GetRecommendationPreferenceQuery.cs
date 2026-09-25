using ErrorOr;
using MediatR;

namespace Vole_Papillon_Damour.Application.Recommendations.Queries.GetRecommendationPreference;

public sealed record GetRecommendationPreferenceQuery(
    string ExternalId,
    string? Email,
    string? FirstName,
    string? LastName) : IRequest<ErrorOr<RecommendationPreferenceResult>>;

public sealed record RecommendationPreferenceResult(bool Enabled);
