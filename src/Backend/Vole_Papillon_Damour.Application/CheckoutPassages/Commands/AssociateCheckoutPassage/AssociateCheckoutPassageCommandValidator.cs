using FluentValidation;

namespace Vole_Papillon_Damour.Application.CheckoutPassages.Commands.AssociateCheckoutPassage;

public sealed class AssociateCheckoutPassageCommandValidator : AbstractValidator<AssociateCheckoutPassageCommand>
{
    public AssociateCheckoutPassageCommandValidator()
    {
        RuleFor(command => command.CheckoutPassageId)
            .NotEmpty();

        RuleFor(command => command.Credential)
            .NotEmpty();

        RuleFor(command => command.VolunteerId)
            .Must(volunteerId => volunteerId is not null && volunteerId.Value != Guid.Empty);
    }
}