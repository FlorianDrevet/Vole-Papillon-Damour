using FluentValidation;

namespace Vole_Papillon_Damour.Application.MemberSelection.Commands.AddSelectionItem;

public sealed class AddSelectionItemCommandValidator : AbstractValidator<AddSelectionItemCommand>
{
    public AddSelectionItemCommandValidator()
    {
        RuleFor(command => command.ExternalId).NotEmpty();
        RuleFor(command => command.Email)
            .NotEmpty()
            .MaximumLength(320);
    }
}
