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
        RuleFor(command => command.Title).MaximumLength(500);
        RuleFor(command => command.Authors).MaximumLength(500);
        RuleFor(command => command.Publisher).MaximumLength(200);
        RuleFor(command => command.PublicationYear)
            .InclusiveBetween(1, 9999)
            .When(command => command.PublicationYear.HasValue);
    }
}
