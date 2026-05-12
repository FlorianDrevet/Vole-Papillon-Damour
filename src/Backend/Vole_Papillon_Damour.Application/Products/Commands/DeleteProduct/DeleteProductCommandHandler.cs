using ErrorOr;
using MediatR;
using Vole_Papillon_Damour.Application.Common.Interfaces.Persistence;

namespace Vole_Papillon_Damour.Application.Products.Commands.DeleteProduct;

public class DeleteProductCommandHandler(IProductRepository productRepository)
    : IRequestHandler<DeleteProductCommand, ErrorOr<bool>>
{
    public async Task<ErrorOr<bool>> Handle(DeleteProductCommand command, CancellationToken cancellationToken)
    {
        return await productRepository.DeleteAsync(command.ProductId);
    }
}