using FluentValidation;
using Vole_Papillon_Damour.Application.Events.Commands.CreateEvent;

namespace Vole_Papillon_Damour.Application.Events.Commands.Parties.AddPartieToEvent;

public class AddPartieToEventCommandValidator : AbstractValidator<AddPartieToEventCommand>
{
    public AddPartieToEventCommandValidator()
    {
        RuleFor(x => x.PartiesCommand)
            .SetInheritanceValidator(v => v.Add(new CreatePartieCommandValidator()));
        RuleFor(x => x.AssoEventsId).NotEmpty();
    }
}