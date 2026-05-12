using FluentValidation;

namespace Vole_Papillon_Damour.Application.Orders.Commands.CreateOrder;

public class CreateOrderCommandValidator : AbstractValidator<CreateOrderCommand>
{
    public CreateOrderCommandValidator()
    {
        RuleFor(x => x.FamilyName).NotEmpty().WithMessage("FamilyName should not be empty");
        RuleFor(x => x.TotalPrice)
            .NotEmpty().WithMessage("Total should not be empty")
            .GreaterThanOrEqualTo(0).WithMessage("Total should be positive");
        RuleFor(x => x.Status)
            .NotEmpty().WithMessage("Status should not be empty");
        RuleFor(x => x.OrderedProduct).NotEmpty().WithMessage("OrderedProducts should not be empty");
        
        RuleFor(x => x.OrderedProduct)
            .ForEach(p => p.SetValidator(new OrderedProductValidator()));
    }
}

public class OrderedProductValidator : AbstractValidator<OrderedProductCommand>
{
    public OrderedProductValidator()
    {
        RuleFor(p => p.ProductId).NotEmpty().WithMessage("ProductId should not be empty");
        RuleFor(p => p.Quantity)
            .NotEmpty().WithMessage("Quantity should not be empty")
            .GreaterThanOrEqualTo(0).WithMessage("Quantity should be greater than 0");
    } 
}