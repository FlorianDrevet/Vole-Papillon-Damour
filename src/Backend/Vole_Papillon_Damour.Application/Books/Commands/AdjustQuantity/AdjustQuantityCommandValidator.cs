using FluentValidation;

namespace Vole_Papillon_Damour.Application.Books.Commands.AdjustQuantity;

public sealed class AdjustQuantityCommandValidator : AbstractValidator<AdjustQuantityCommand>
{
    public AdjustQuantityCommandValidator()
    {
        RuleFor(command => command.Isbn).NotEmpty();
        RuleFor(command => command.QuantityAvailable).GreaterThanOrEqualTo(0);
        RuleFor(command => command.Note)
            .NotEmpty()
            .MaximumLength(500);
        RuleFor(command => command.VolunteerId).NotNull();
    }
}
