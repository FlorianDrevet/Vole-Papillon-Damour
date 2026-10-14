using FluentValidation;

namespace Vole_Papillon_Damour.Application.ManualRequests.Commands.Refuse;

public sealed class RefuseManualRequestCommandValidator : AbstractValidator<RefuseManualRequestCommand>
{
    public RefuseManualRequestCommandValidator()
    {
        RuleFor(command => command.RequestId).NotEmpty();
        RuleFor(command => command.By).NotNull();
    }
}
