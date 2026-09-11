using MapsterMapper;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Vole_Papillon_Damour.Api.Errors;
using Vole_Papillon_Damour.Application.Actuality.Commands.AddActuality;
using Vole_Papillon_Damour.Application.Actuality.Commands.DeleteActuality;
using Vole_Papillon_Damour.Application.Actuality.Commands.PublishActuality;
using Vole_Papillon_Damour.Application.Actuality.Commands.UpdateActuality;
using Vole_Papillon_Damour.Application.Actuality.Queries;
using Vole_Papillon_Damour.Application.Actuality.Queries.GetActualityById;
using Vole_Papillon_Damour.Application.Actuality.Queries.GetDraftActualities;
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
    private const string AdminPolicyName = "IsAdmin";

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
                .RequireAuthorization("IsAdmin");

            endpoints.MapGet("/actuality/all",
                    async (
                        bool? includeDrafts,
                        HttpContext httpContext,
                        IAuthorizationService authorizationService,
                        IMediator mediator,
                        IMapper mapper) =>
                    {
                        var includeDraftsValue = includeDrafts ?? false;
                        if (includeDraftsValue)
                        {
                            var authorization = await authorizationService.AuthorizeAsync(
                                httpContext.User,
                                resource: null,
                                policyName: AdminPolicyName);

                            if (!authorization.Succeeded)
                            {
                                return Results.Forbid();
                            }
                        }

                        var command = new GetAllActualityQuery(includeDraftsValue);
                        var commandResult = await mediator.Send(command);

                        return commandResult.Match(
                            result =>
                            {
                                var user = mapper.Map<List<ActualityResponse>>(result);
                                return Results.Ok(user);
                            },
                            error => error.Result());
                    })
                .WithName("Get all the actuality");

            endpoints.MapGet("/actuality/drafts",
                    async (IMediator mediator, IMapper mapper) =>
                    {
                        var queryResult = await mediator.Send(new GetDraftActualitiesQuery());

                        return queryResult.Match(
                            result => Results.Ok(mapper.Map<List<ActualityResponse>>(result)),
                            error => error.Result());
                    })
                .WithName("Get actuality drafts")
                .RequireAuthorization(AdminPolicyName);

            endpoints.MapPost("/actuality/{id}/publish",
                    async (Guid id, IMediator mediator, IMapper mapper) =>
                    {
                        var commandResult = await mediator.Send(
                            new PublishActualityCommand(ActualityId.Create(id)));

                        return commandResult.Match(
                            result => Results.Ok(mapper.Map<ActualityResponse>(result)),
                            error => error.Result());
                    })
                .WithName("Publish an actuality")
                .RequireAuthorization(AdminPolicyName);
            
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
                .WithName("Get the 3 latest actuality");
            
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
                .WithName("Get an actuality by its id");
 
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
                .RequireAuthorization("IsAdmin");

            endpoints.MapPut("/actuality/{id}",
                    async ([FromForm] UpdateActualityRequest request, Guid id, IMediator mediator, IMapper mapper) =>
                    {
                        var command = mapper.Map<UpdateActualityCommand>((request, id));
                        var commandResult = await mediator.Send(command);

                        return commandResult.Match(
                            result =>
                            {
                                return Results.Ok(mapper.Map<ActualityResponse>(result));
                            },
                            error => error.Result());
                    })
                .WithName("Update an actuality with its id")
                .DisableAntiforgery()
                .RequireAuthorization("IsAdmin");
        });
    }
}
