using FluentValidation;

namespace Vole_Papillon_Damour.Application.Books.Commands.PermanentlyDeleteBook;

public sealed class PermanentlyDeleteBookCommandValidator : AbstractValidator<PermanentlyDeleteBookCommand>
{
    public PermanentlyDeleteBookCommandValidator()
    {
        RuleFor(command => command.Isbn).NotEmpty();
        RuleFor(command => command.DeletedBy).NotNull();
    }
}
