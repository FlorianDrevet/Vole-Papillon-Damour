using FluentValidation;

namespace Vole_Papillon_Damour.Application.WatchlistFeature.Commands.RemoveWatchlistItem;

public sealed class RemoveWatchlistItemCommandValidator : AbstractValidator<RemoveWatchlistItemCommand>
{
    public RemoveWatchlistItemCommandValidator()
    {
        RuleFor(command => command.ExternalId).NotEmpty();
        RuleFor(command => command.Email)
            .NotEmpty()
            .MaximumLength(320);
        RuleFor(command => command.ItemId).NotEmpty();
    }
}
