using FluentValidation;

namespace Vole_Papillon_Damour.Application.ManualRequests.Commands.CreateRequester;

public sealed class CreateManualRequesterCommandValidator : AbstractValidator<CreateManualRequesterCommand>
{
    public CreateManualRequesterCommandValidator()
    {
        RuleFor(command => command.LastName).NotEmpty();
        RuleFor(command => command.FirstName).NotEmpty();
        RuleFor(command => command.Phone).NotEmpty();
    }
}
