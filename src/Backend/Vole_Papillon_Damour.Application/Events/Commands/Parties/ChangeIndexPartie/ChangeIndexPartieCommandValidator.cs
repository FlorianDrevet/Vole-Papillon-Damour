using FluentValidation;

namespace Vole_Papillon_Damour.Application.Events.Commands.Parties.ChangeIndexPartie;

public class ChangeIndexPartieCommandValidator : AbstractValidator<ChangeIndexPartieCommand>
{
    public ChangeIndexPartieCommandValidator()
    {
        RuleFor(x => x.AssoEventsId).NotEmpty();
        RuleFor(x => x.PartieId).NotEmpty();
    }
}