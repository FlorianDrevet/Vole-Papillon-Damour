using FluentValidation;

namespace Vole_Papillon_Damour.Application.ManualRequests.Commands.DeleteRequester;

public sealed class DeleteManualRequesterCommandValidator : AbstractValidator<DeleteManualRequesterCommand>
{
    public DeleteManualRequesterCommandValidator()
    {
        RuleFor(command => command.RequesterId).NotEmpty();
        RuleFor(command => command.By).NotNull();
    }
}
