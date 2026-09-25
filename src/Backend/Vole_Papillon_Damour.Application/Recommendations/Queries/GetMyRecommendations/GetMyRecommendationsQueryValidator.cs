using FluentValidation;
using Vole_Papillon_Damour.Application.Common.Interfaces.Services;

namespace Vole_Papillon_Damour.Application.Recommendations.Queries.GetMyRecommendations;

public sealed class GetMyRecommendationsQueryValidator : AbstractValidator<GetMyRecommendationsQuery>
{
    public GetMyRecommendationsQueryValidator(IRecommendationSettings settings)
    {
        RuleFor(query => query.ExternalId)
            .NotEmpty();

        RuleFor(query => query.Email)
            .NotEmpty();

        RuleFor(query => query.Limit)
            .InclusiveBetween(1, settings.PersonalMaxCount);
    }
}
