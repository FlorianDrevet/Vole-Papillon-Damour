using FluentValidation;

namespace Vole_Papillon_Damour.Application.Books.Commands.ScanBook;

public sealed class ScanBookCommandValidator : AbstractValidator<ScanBookCommand>
{
    public ScanBookCommandValidator()
    {
        RuleFor(command => command.Isbn).NotEmpty();
        RuleFor(command => command.ScanSessionId).NotNull();
        RuleFor(command => command.ClientGestureId).NotEmpty();
        RuleFor(command => command.OccurredAt)
            .Must(timestamp => timestamp.Kind == DateTimeKind.Utc)
            .WithMessage("OccurredAt must be expressed in UTC.");
    }
}
