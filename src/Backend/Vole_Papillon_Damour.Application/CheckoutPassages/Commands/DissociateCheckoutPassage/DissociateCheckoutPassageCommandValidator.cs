using FluentValidation;

namespace Vole_Papillon_Damour.Application.CheckoutPassages.Commands.DissociateCheckoutPassage;

public sealed class DissociateCheckoutPassageCommandValidator : AbstractValidator<DissociateCheckoutPassageCommand>
{
    public DissociateCheckoutPassageCommandValidator()
    {
        RuleFor(command => command.CheckoutPassageId)
            .NotEmpty();

        RuleFor(command => command.AdministratorId)
            .Must(administratorId => administratorId is not null && administratorId.Value != Guid.Empty);

        RuleFor(command => command.Reason)
            .Must(reason => reason is not null && reason.Trim().Length is >= 3 and <= 500)
            .WithMessage("La raison doit contenir entre 3 et 500 caractères.");
    }
}
