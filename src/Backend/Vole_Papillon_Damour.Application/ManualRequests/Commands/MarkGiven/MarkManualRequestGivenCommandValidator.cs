using FluentValidation;

namespace Vole_Papillon_Damour.Application.ManualRequests.Commands.MarkGiven;

public sealed class MarkManualRequestGivenCommandValidator : AbstractValidator<MarkManualRequestGivenCommand>
{
    public MarkManualRequestGivenCommandValidator()
    {
        RuleFor(command => command.RequestId).NotEmpty();
        RuleFor(command => command.By).NotNull();
    }
}
