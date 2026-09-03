using FluentValidation;

namespace Vole_Papillon_Damour.Application.Books.Commands.DeleteBook;

public sealed class DeleteBookCommandValidator : AbstractValidator<DeleteBookCommand>
{
    public DeleteBookCommandValidator()
    {
        RuleFor(command => command.Isbn).NotEmpty();
        RuleFor(command => command.DeletedBy).NotNull();
    }
}
