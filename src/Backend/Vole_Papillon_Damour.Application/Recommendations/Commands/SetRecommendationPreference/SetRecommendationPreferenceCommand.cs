using ErrorOr;
using MediatR;

namespace Vole_Papillon_Damour.Application.Recommendations.Commands.SetRecommendationPreference;

public sealed record SetRecommendationPreferenceCommand(
    string ExternalId,
    string? Email,
    string? FirstName,
    string? LastName,
    bool Enabled) : IRequest<ErrorOr<Success>>;
