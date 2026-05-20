using FluentValidation;
using Vole_Papillon_Damour.Domain.ProductAggregate.ValueObjects;

namespace Vole_Papillon_Damour.Application.Products.Commands.CreateProduct;

public class ProductCommandValidator : AbstractValidator<CreateProductCommand>
{
    public ProductCommandValidator()
    {
        RuleFor(x => x.Name).NotEmpty();
        RuleFor(x => x.Price).GreaterThan(0);
        RuleFor(x => x.ImageName).NotEmpty();
        RuleFor(x => x.Image).NotEmpty();
        RuleFor(x => x.ProductCategory).NotEmpty()
            .When(x => x.ProductSection.Value == ProductSection.ProductSectionEnum.Bar)
            .WithMessage("ProductCategory is required for Bar section");
        RuleFor(x => x.ProductSection.Value).IsInEnum();
    }
}
