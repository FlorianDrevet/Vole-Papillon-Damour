using FluentValidation;

namespace Vole_Papillon_Damour.Application.Books.Commands.CancelBookAlerts;

public sealed class CancelBookAlertsCommandValidator : AbstractValidator<CancelBookAlertsCommand>
{
    public CancelBookAlertsCommandValidator()
    {
        RuleFor(command => command.ScanSessionId).NotNull();
        RuleFor(command => command.UpdatedBy).NotNull();
    }
}
