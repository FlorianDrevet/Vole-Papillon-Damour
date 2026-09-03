using FluentValidation;

namespace Vole_Papillon_Damour.Application.Books.Commands.BookFlags;

public sealed class MarkBookRareCommandValidator : AbstractValidator<MarkBookRareCommand>
{
    public MarkBookRareCommandValidator()
    {
        RuleFor(command => command.Isbn).NotEmpty();
        RuleFor(command => command.UpdatedBy).NotNull();
    }
}

public sealed class HideBookCommandValidator : AbstractValidator<HideBookCommand>
{
    public HideBookCommandValidator()
    {
        RuleFor(command => command.Isbn).NotEmpty();
        RuleFor(command => command.UpdatedBy).NotNull();
    }
}
