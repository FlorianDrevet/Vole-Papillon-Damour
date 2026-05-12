using MapsterMapper;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using Vole_Papillon_Damour.Api.Errors;
using Vole_Papillon_Damour.Application.Actuality.Commands.AddActuality;
using Vole_Papillon_Damour.Application.Actuality.Commands.DeleteActuality;
using Vole_Papillon_Damour.Application.Actuality.Commands.UpdateActuality;
using Vole_Papillon_Damour.Application.Actuality.Queries;
using Vole_Papillon_Damour.Application.Actuality.Queries.GetActualityById;
using Vole_Papillon_Damour.Application.Actuality.Queries.GetAllActuality;
using Vole_Papillon_Damour.Application.Authentication.Commands.Register;
using Vole_Papillon_Damour.Application.Authentication.Queries.Login;
using Vole_Papillon_Damour.Contracts.Actuality.Requests;
using Vole_Papillon_Damour.Contracts.Actuality.Responses;
using Vole_Papillon_Damour.Contracts.Authentication;
using Vole_Papillon_Damour.Contracts.Authentication.Requests;
using Vole_Papillon_Damour.Contracts.Authentication.Responses;
using Vole_Papillon_Damour.Domain.ActualityAggregate.ValueObjects;

namespace Vole_Papillon_Damour.Api.Controllers;

public static class ActualityController
{
    public static IApplicationBuilder UseActualityController(this IApplicationBuilder builder)
    {
        return builder.UseEndpoints(endpoints =>
        {
            endpoints.MapPost("/actuality",
                    async ([FromForm] CreateActualityRequest request, IMediator mediator, IMapper mapper) =>
                    {
                        var command = mapper.Map<AddACtualityCommand>(request);
                        var commandResult = await mediator.Send(command);

                        return commandResult.Match(
                            result =>
                            {
                                var user = mapper.Map<ActualityResponse>(result);
                                return Results.Ok(user);
                            },
                            error => error.Result());
                    })
                .WithName("Add a new actuality")
                .DisableAntiforgery()
                .RequireAuthorization("IsAdmin")
                .WithOpenApi();

            endpoints.MapGet("/actuality/all",
                    async (IMediator mediator, IMapper mapper) =>
                    {
                        var command = new GetAllActualityQuery();
                        var commandResult = await mediator.Send(command);

                        return commandResult.Match(
                            result =>
                            {
                                var user = mapper.Map<List<ActualityResponse>>(result);
                                return Results.Ok(user);
                            },
                            error => error.Result());
                    })
                .WithName("Get all the actuality")
                .WithOpenApi();
            
            endpoints.MapGet("/actuality/latest",
                    async (IMediator mediator, IMapper mapper) =>
                    {
                        var command = new GetLatestActualityQuery();
                        var commandResult = await mediator.Send(command);

                        return commandResult.Match(
                            result =>
                            {
                                var user = mapper.Map<List<ActualityResponse>>(result);
                                return Results.Ok(user);
                            },
                            error => error.Result());
                    })
                .WithName("Get the 3 latest actuality")
                .WithOpenApi();
            
            endpoints.MapGet("/actuality/{id}",
                    async (Guid id, IMediator mediator, IMapper mapper) =>
                    {
                        var command = new GetActualityByIdQuery(new ActualityId(id));
                        var commandResult = await mediator.Send(command);

                        return commandResult.Match(
                            result =>
                            {
                                var user = mapper.Map<ActualityResponse>(result);
                                return Results.Ok(user);
                            },
                            error => error.Result());
                    })
                .WithName("Get an actuality by its id")
                .WithOpenApi();
 
            endpoints.MapDelete("/actuality/{id}",
                    async (Guid id, IMediator mediator, IMapper mapper) =>
                    {
                        var command = new DeleteActualityCommand(new ActualityId(id));
                        var commandResult = await mediator.Send(command);

                        return commandResult.Match(
                            result =>
                            {
                                return Results.Ok(result);
                            },
                            error => error.Result());
                    })
                .WithName("Delete an actuality with its id")
                .RequireAuthorization("IsAdmin")
                .WithOpenApi();

            endpoints.MapPut("/actuality/{id}",
                    async ([FromForm] UpdateActualityRequest request, Guid id, IMediator mediator, IMapper mapper) =>
                    {
                        var command = mapper.Map<UpdateActualityCommand>((request, id));
                        var commandResult = await mediator.Send(command);

                        return commandResult.Match(
                            result =>
                            {
                                return Results.Ok(result);
                            },
                            error => error.Result());
                    })
                .WithName("Update an actuality with its id")
                .DisableAntiforgery()
                .RequireAuthorization("IsAdmin")
                .WithOpenApi();
        });
    }
}