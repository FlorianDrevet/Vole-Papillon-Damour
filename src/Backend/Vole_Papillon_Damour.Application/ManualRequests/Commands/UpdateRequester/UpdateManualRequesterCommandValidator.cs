using FluentValidation;

namespace Vole_Papillon_Damour.Application.ManualRequests.Commands.UpdateRequester;

public sealed class UpdateManualRequesterCommandValidator : AbstractValidator<UpdateManualRequesterCommand>
{
    public UpdateManualRequesterCommandValidator()
    {
        RuleFor(command => command.RequesterId).NotEmpty();
        RuleFor(command => command.LastName).NotEmpty();
        RuleFor(command => command.FirstName).NotEmpty();
        RuleFor(command => command.Phone).NotEmpty();
    }
}
