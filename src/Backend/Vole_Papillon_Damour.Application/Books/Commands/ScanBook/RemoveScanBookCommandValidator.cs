using FluentValidation;

namespace Vole_Papillon_Damour.Application.Books.Commands.ScanBook;

public sealed class RemoveScanBookCommandValidator : AbstractValidator<RemoveScanBookCommand>
{
    public RemoveScanBookCommandValidator()
    {
        RuleFor(command => command.ScanSessionId).NotNull();
        RuleFor(command => command.ClientGestureId).NotEmpty();
        RuleFor(command => command.VolunteerId).NotNull();
    }
}
