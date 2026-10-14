using FluentValidation;

namespace Vole_Papillon_Damour.Application.ManualRequests.Commands.Cancel;

public sealed class CancelManualRequestCommandValidator : AbstractValidator<CancelManualRequestCommand>
{
    public CancelManualRequestCommandValidator()
    {
        RuleFor(command => command.RequestId).NotEmpty();
        RuleFor(command => command.By).NotNull();
    }
}
