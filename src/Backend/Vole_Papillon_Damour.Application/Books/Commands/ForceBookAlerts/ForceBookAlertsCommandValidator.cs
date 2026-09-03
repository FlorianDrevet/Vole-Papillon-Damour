using FluentValidation;

namespace Vole_Papillon_Damour.Application.Books.Commands.ForceBookAlerts;

public sealed class ForceBookAlertsCommandValidator : AbstractValidator<ForceBookAlertsCommand>
{
    public ForceBookAlertsCommandValidator()
    {
        RuleFor(command => command.ScanSessionId).NotNull();
        RuleFor(command => command.UpdatedBy).NotNull();
    }
}
