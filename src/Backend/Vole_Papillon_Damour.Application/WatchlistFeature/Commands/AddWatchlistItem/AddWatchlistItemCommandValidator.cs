using FluentValidation;

namespace Vole_Papillon_Damour.Application.WatchlistFeature.Commands.AddWatchlistItem;

public sealed class AddWatchlistItemCommandValidator : AbstractValidator<AddWatchlistItemCommand>
{
    public AddWatchlistItemCommandValidator()
    {
        RuleFor(command => command.ExternalId).NotEmpty();
        RuleFor(command => command.Email)
            .NotEmpty()
            .MaximumLength(320);
    }
}
