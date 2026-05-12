using ErrorOr;
using MediatR;
using Microsoft.AspNetCore.Http;
using Vole_Papillon_Damour.Application.Products.Common;
using Vole_Papillon_Damour.Domain.ProductAggregate.ValueObjects;

namespace Vole_Papillon_Damour.Application.Products.Commands.UpdateProduct;

public record UpdateProductCommand(
    ProductId ProductId,
    string Name,
    double Price,
    IFormFile? Image,
    Uri? UrlImage,
    bool Available,
    ProductCategory? ProductCategory,
    ProductSection ProductSection
) : IRequest<ErrorOr<ProductResult>>;