using FluentValidation;

namespace Vole_Papillon_Damour.Application.Events.Commands.Numeros.AddNumeroToEvent;

public class AddNumeroToEventCommandValidator : AbstractValidator<AddNumeroToEventCommand>
{
    public AddNumeroToEventCommandValidator()
    {
        RuleFor(x => x.Numero)
            .GreaterThanOrEqualTo(1)
            .LessThanOrEqualTo(90)
            .WithMessage("Numero should be between 1 and 90");

        RuleFor(x => x.AssoEventsId)
            .NotEmpty()
            .WithMessage("AssoEventsId should not be empty");
    }
}