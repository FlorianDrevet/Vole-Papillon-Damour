using MapsterMapper;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using Vole_Papillon_Damour.Api.Errors;
using Vole_Papillon_Damour.Application.Orders.Commands.CreateOrder;
using Vole_Papillon_Damour.Application.Orders.Queries.GetAllOrdersQuery;
using Vole_Papillon_Damour.Contracts.Order.Requests;
using Vole_Papillon_Damour.Contracts.Order.Responses;
using Vole_Papillon_Damour.Domain.OrderAggregate.ValueObjects;

namespace Vole_Papillon_Damour.Api.Controllers;

public static class OrdersController
{
    public static IApplicationBuilder UseOrdersController(this IApplicationBuilder builder)
    {
        return builder.UseEndpoints(endpoints =>
        {
            endpoints.MapPost("/orders",
                    async ([FromBody] CreateOrderRequests request,
                        IMediator mediator, IMapper mapper) =>
                    {
                        var command = mapper.Map<CreateOrderCommand>(request);

                        if (command.Status.Value == StatusEnum.StatusEnumEnum.Unknown)
                        {
                            return Results.BadRequest("Invalid status");
                        }
                        
                        var createOrderResult = await mediator.Send(command);

                        return createOrderResult.Match(
                            createOrderResult =>
                            {
                                var eventResponse = mapper.Map<OrderResponse>(createOrderResult);
                                return Results.Ok(eventResponse);
                            },
                            error => error.Result());
                    })
                .WithName("Create an order")
                .RequireAuthorization("IsAdmin");
            
            endpoints.MapGet("/orders",
                    async (IMediator mediator, IMapper mapper) =>
                    {
                        var query = new GetAllOrdersQuery();
                        
                        var getAllOrderResult = await mediator.Send(query);

                        return getAllOrderResult.Match(
                            getAllOrderResult =>
                            {
                                var eventResponse = mapper.Map<List<OrderResponse>>(getAllOrderResult);
                                return Results.Ok(eventResponse);
                            },
                            error => error.Result());
                    })
                .WithName("Get all Orders")
                .RequireAuthorization("IsAdmin");
        });
    }
}