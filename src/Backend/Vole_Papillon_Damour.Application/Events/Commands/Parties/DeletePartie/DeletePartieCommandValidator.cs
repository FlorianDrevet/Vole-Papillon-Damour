using FluentValidation;

namespace Vole_Papillon_Damour.Application.Events.Commands.Parties.DeletePartie;

public class DeletePartieCommandValidator : AbstractValidator<DeletePartieCommand>
{
    public DeletePartieCommandValidator()
    {
        RuleFor(x => x.AssoEventsId).NotEmpty();
        RuleFor(x => x.PartieId).NotEmpty();
    }
}