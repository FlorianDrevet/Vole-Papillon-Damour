using MapsterMapper;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using Vole_Papillon_Damour.Api.Errors;
using Vole_Papillon_Damour.Application.Common.Interfaces.Services;
using Vole_Papillon_Damour.Application.Products.Commands.AddPromotions;
using Vole_Papillon_Damour.Application.Products.Commands.CreateProduct;
using Vole_Papillon_Damour.Application.Products.Commands.DeleteProduct;
using Vole_Papillon_Damour.Application.Products.Commands.DeletePromotions;
using Vole_Papillon_Damour.Application.Products.Commands.UpdateProduct;
using Vole_Papillon_Damour.Application.Products.Queries.GetAllProduct;
using Vole_Papillon_Damour.Application.Products.Queries.GetPublicProducts;
using Vole_Papillon_Damour.Contracts.Authentication;
using Vole_Papillon_Damour.Contracts.Product.Requests;
using Vole_Papillon_Damour.Contracts.Product.Responses;
using Vole_Papillon_Damour.Domain.ProductAggregate.ValueObjects;

namespace Vole_Papillon_Damour.Api.Controllers;

public static class ProductController
{
    public static IApplicationBuilder UseProductController(this IApplicationBuilder builder)
    {
        return builder.UseEndpoints(endpoints =>
        {
            #region Product
            endpoints.MapPut("/product/{id:guid}",
                    async ([FromForm] UpdateProductRequest request,
                        [FromRoute] Guid id,
                        IMediator mediator, IMapper mapper) =>
                    {
                        if (request.UrlImage is null)
                        {
                            if (request.Image is null || request.Image.Length == 0)
                            {
                                return Results.BadRequest("Image file is required.");
                            }
                        }
                        
                        var command = mapper.Map<UpdateProductCommand>((request, id));
                        var createProductResult = await mediator.Send(command);

                        return createProductResult.Match(
                            createProductResult =>
                            {
                                var user = mapper.Map<ProductResponse>(createProductResult);
                                return Results.Ok(user);
                            },
                            error => error.Result());
                    })
                .WithName("Update existing Product")
                .RequireAuthorization("IsAdmin")
                .DisableAntiforgery();
            
            endpoints.MapPost("/product",
                    async ([FromForm] CreateProductRequest request,
                        IMediator mediator, IMapper mapper) =>
                    {
                        if (request.Image is null || request.Image.Length == 0)
                        {
                            return Results.BadRequest("Image file is required.");
                        }
                        
                        var command = mapper.Map<CreateProductCommand>(request);
                        var createProductResult = await mediator.Send(command);

                        return createProductResult.Match(
                            createProductResult =>
                            {
                                var user = mapper.Map<ProductResponse>(createProductResult);
                                return Results.Ok(user);
                            },
                            error => error.Result());
                    })
                .WithName("Create New Product")
                .RequireAuthorization("IsAdmin")
                .DisableAntiforgery();
            
            endpoints.MapGet("/product",
                    async (IMediator mediator, IMapper mapper) =>
                    {
                        var query = new GetAllProductQuery();
                        var getAllProductResult = await mediator.Send(query);

                        return getAllProductResult.Match(
                            getAllProductResult =>
                            {
                                var user = mapper.Map<List<ProductResponse>>(getAllProductResult);
                                return Results.Ok(user);
                            },
                            error => error.Result());
                    })
                .WithName("Get All Products");

            endpoints.MapGet("/product/public",
                    async (IMediator mediator, IMapper mapper) =>
                    {
                        var query = new GetPublicProductsQuery();
                        var getPublicProductsResult = await mediator.Send(query);

                        return getPublicProductsResult.Match(
                            getPublicProductsResult =>
                            {
                                var products = mapper.Map<List<ProductResponse>>(getPublicProductsResult);
                                return Results.Ok(products);
                            },
                            error => error.Result());
                    })
                .WithName("Get Public Products");
            
            endpoints.MapDelete("/product/{productId}",
                    async ([FromRoute] Guid productId, IMediator mediator, IMapper mapper) =>
                    {
                        var command = new DeleteProductCommand(ProductId.Create(productId));
                        var deleteProductResult = await mediator.Send(command);

                        return deleteProductResult.Match(
                            deleteProductResult =>
                            {
                                return Results.Ok(deleteProductResult);
                            },
                            error => error.Result());
                    })
                .WithName("Delete Product");
            #endregion

            
            endpoints.MapPost("/product/promotion",
                    async (AddPromotionRequest request,
                        IMediator mediator, IMapper mapper) =>
                    {
                        var command = mapper.Map<AddPromotionCommand>(request);
                        var addPromotionResult = await mediator.Send(command);

                        return addPromotionResult.Match(
                            addPromotionResult =>
                            {
                                var user = mapper.Map<ProductResponse>(addPromotionResult);
                                return Results.Ok(user);
                            },
                            error => error.Result());
                    })
                .WithName("Add promotion to a Product")
                .RequireAuthorization("IsAdmin");
            
                        
            endpoints.MapDelete("/product/promotion",
                    async ([FromBody] DeletePromotionRequest request,
                        IMediator mediator, IMapper mapper) =>
                    {
                        var command = mapper.Map<DeletePromotionCommand>(request);
                        var deletePromotionResult = await mediator.Send(command);

                        return deletePromotionResult.Match(
                            deletePromotionResult =>
                            {
                                return Results.Ok(deletePromotionResult);
                            },
                            error => error.Result());
                    })
                .WithName("Delete a promotion")
                .RequireAuthorization("IsAdmin");
        });
    }
}
