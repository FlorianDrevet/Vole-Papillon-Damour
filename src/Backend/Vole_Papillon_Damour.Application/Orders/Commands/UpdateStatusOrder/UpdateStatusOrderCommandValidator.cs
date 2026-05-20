using FluentValidation;

namespace Vole_Papillon_Damour.Application.Orders.Commands.UpdateStatusOrder;

public class UpdateStatusOrderCommandValidator : AbstractValidator<UpdateStatusOrderCommand>
{
    public UpdateStatusOrderCommandValidator()
    {
        RuleFor(x => x.Status).NotEmpty().WithName("Status should not be empty");
        RuleFor(x => x.OrderId).NotEmpty().WithName("OrderId should not be empty");
        
    }
}