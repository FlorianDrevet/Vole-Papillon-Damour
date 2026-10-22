using FluentValidation;

namespace Vole_Papillon_Damour.Application.AccountAdministration;

public sealed class ResetAdminAccountPasswordCommandValidator
    : AbstractValidator<ResetAdminAccountPasswordCommand>
{
    public ResetAdminAccountPasswordCommandValidator()
    {
        RuleFor(command => command.TargetExternalId).NotEmpty();
    }
}
