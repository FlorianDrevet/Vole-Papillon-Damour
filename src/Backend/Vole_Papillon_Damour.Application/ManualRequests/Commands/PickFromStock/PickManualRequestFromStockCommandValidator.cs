using FluentValidation;

namespace Vole_Papillon_Damour.Application.ManualRequests.Commands.PickFromStock;

public sealed class PickManualRequestFromStockCommandValidator : AbstractValidator<PickManualRequestFromStockCommand>
{
    public PickManualRequestFromStockCommandValidator()
    {
        RuleFor(command => command.RequestId).NotEmpty();
        RuleFor(command => command.By).NotNull();
    }
}
