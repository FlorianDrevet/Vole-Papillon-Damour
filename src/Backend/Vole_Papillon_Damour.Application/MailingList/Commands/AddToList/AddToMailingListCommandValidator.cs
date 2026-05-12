using FluentValidation;

namespace Vole_Papillon_Damour.Application.MailingList.Commands.AddToList;

public class AddToMailingListCommandValidator : AbstractValidator<AddToMailingListCommand>
{
    public AddToMailingListCommandValidator()
    {
        RuleFor(x => x.Email)
            .NotEmpty().WithMessage("Email is required.")
            .EmailAddress().WithMessage("Email is not valid.");
    }
}