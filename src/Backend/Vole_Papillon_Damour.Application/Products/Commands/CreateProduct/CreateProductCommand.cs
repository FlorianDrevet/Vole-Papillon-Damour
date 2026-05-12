using ErrorOr;
using MediatR;
using Vole_Papillon_Damour.Application.Products.Common;
using Vole_Papillon_Damour.Domain.ProductAggregate.ValueObjects;

namespace Vole_Papillon_Damour.Application.Products.Commands.CreateProduct;

public record CreateProductCommand(
    string Name,
    double Price,
    Stream Image,
    string ImageName,
    bool Available,
    ProductSection ProductSection,
    ProductCategory? ProductCategory = null
    ): IRequest<ErrorOr<ProductResult>>;