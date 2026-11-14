using FluentValidation;

namespace Vole_Papillon_Damour.Application.Recommendations.Commands.SetRecommendationPreference;

public sealed class SetRecommendationPreferenceCommandValidator : AbstractValidator<SetRecommendationPreferenceCommand>
{
    public SetRecommendationPreferenceCommandValidator()
    {
        RuleFor(command => command.ExternalId)
            .NotEmpty();

        RuleFor(command => command.Email)
            .NotEmpty();

        RuleFor(command => command.MinimumHearts)
            .InclusiveBetween(1, 4)
            .When(command => command.MinimumHearts.HasValue);
    }
}
