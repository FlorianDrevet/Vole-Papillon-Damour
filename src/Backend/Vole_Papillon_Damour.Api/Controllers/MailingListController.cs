using MapsterMapper;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using Vole_Papillon_Damour.Api.Errors;
using Vole_Papillon_Damour.Application.Common.Interfaces.Services;
using Vole_Papillon_Damour.Application.MailingList;
using Vole_Papillon_Damour.Application.MailingList.Commands.DeleteFromList;
using Vole_Papillon_Damour.Application.Products.Commands.AddPromotions;
using Vole_Papillon_Damour.Application.Products.Commands.CreateProduct;
using Vole_Papillon_Damour.Application.Products.Commands.DeleteProduct;
using Vole_Papillon_Damour.Application.Products.Commands.DeletePromotions;
using Vole_Papillon_Damour.Application.Products.Commands.UpdateProduct;
using Vole_Papillon_Damour.Application.Products.Queries.GetAllProduct;
using Vole_Papillon_Damour.Contracts.Authentication;
using Vole_Papillon_Damour.Contracts.MailingList.Requests;
using Vole_Papillon_Damour.Contracts.Product.Requests;
using Vole_Papillon_Damour.Contracts.Product.Responses;
using Vole_Papillon_Damour.Domain.ProductAggregate.ValueObjects;

namespace Vole_Papillon_Damour.Api.Controllers;

public static class MailingController
{
    public static IApplicationBuilder UseMailingListController(this IApplicationBuilder builder)
    {
        return builder.UseEndpoints(endpoints =>
        {
            endpoints.MapPost("/mailing-list",
                    async (AddToMailingListRequest request,
                        IMediator mediator, IMapper mapper) =>
                    {
                        var command = mapper.Map<AddToMailingListCommand>(request);
                        var addToMailingList = await mediator.Send(command);

                        return addToMailingList.Match(
                            res => Results.Created(),
                            error => error.Result());
                    })
                .WithName("Add to Mailing List")
                .WithOpenApi();
                        
            endpoints.MapDelete("/mailing-list/{email}",
                    async (string email,
                        IMediator mediator) =>
                    {
                        var command = new DeleteFromMailingListCommand(email);
                        var deleteFromMailingList = await mediator.Send(command);

                        return deleteFromMailingList.Match(
                            deletePromotionResult => Results.NoContent(),
                            error => error.Result());
                    })
                .WithName("Delete from Mailing List")
                .WithOpenApi();
        });
    }
}