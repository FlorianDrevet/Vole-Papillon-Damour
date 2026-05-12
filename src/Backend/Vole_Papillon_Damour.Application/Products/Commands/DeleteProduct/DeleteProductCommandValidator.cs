using FluentValidation;

namespace Vole_Papillon_Damour.Application.Products.Commands.DeleteProduct;

public class DeleteProductCommandValidator : AbstractValidator<DeleteProductCommand>
{
    public DeleteProductCommandValidator()
    {
        RuleFor(x => x.ProductId.Value).NotEmpty();
    }
}