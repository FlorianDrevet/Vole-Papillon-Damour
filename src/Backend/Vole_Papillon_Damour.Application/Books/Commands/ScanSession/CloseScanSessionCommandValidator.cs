using FluentValidation;

namespace Vole_Papillon_Damour.Application.Books.Commands.ScanSession;

public sealed class CloseScanSessionCommandValidator : AbstractValidator<CloseScanSessionCommand>
{
    public CloseScanSessionCommandValidator()
    {
        RuleFor(command => command.ScanSessionId).NotNull();
        RuleFor(command => command.CloseReason).IsInEnum();
    }
}
