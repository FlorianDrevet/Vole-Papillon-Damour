using System.Text.Json;
using MapsterMapper;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using Vole_Papillon_Damour.Api.Common.Utils;
using Vole_Papillon_Damour.Api.Errors;
using Vole_Papillon_Damour.Application.Actuality.Commands.DeleteActuality;
using Vole_Papillon_Damour.Application.Common.Interfaces.Services;
using Vole_Papillon_Damour.Application.Events.Commands.CreateEvent;
using Vole_Papillon_Damour.Application.Events.Commands.Events.AddBingoWin;
using Vole_Papillon_Damour.Application.Events.Commands.Events.AddWinPartie;
using Vole_Papillon_Damour.Application.Events.Commands.Events.DeleteEvent;
using Vole_Papillon_Damour.Application.Events.Commands.Events.UpdateEvent;
using Vole_Papillon_Damour.Application.Events.Commands.Numeros.AddNumeroToEvent;
using Vole_Papillon_Damour.Application.Events.Commands.RemoveLastNumero;
using Vole_Papillon_Damour.Application.Events.Queries;
using Vole_Papillon_Damour.Application.Events.Queries.GetAssoEventById;
using Vole_Papillon_Damour.Application.Events.Queries.GetNextBingo;
using Vole_Papillon_Damour.Application.Events.Queries.GetNextBooks;
using Vole_Papillon_Damour.Contracts.Events.Requests;
using Vole_Papillon_Damour.Contracts.Events.Responses;
using Vole_Papillon_Damour.Domain.EventsAggregate.ValueObjects;
using Vole_Papillon_Damour.Infrastructure.Services;

namespace Vole_Papillon_Damour.Api.Controllers.AssoEventsController;

public static class EventsController
{
    public static IApplicationBuilder UseEventsController(this IApplicationBuilder builder)
    {
        builder.UseLotsController();
        builder.UseLinePartieController();
        builder.UsePartieController();
        return builder.UseEndpoints(endpoints =>
        {
            endpoints.MapPost("/asso-events",
                    async ([FromForm] CreateEventRequest request, IMediator mediator, IMapper mapper) =>
                    {
                        var command = mapper.Map<CreateEventCommand>(request);
                        var createCommandResult = await mediator.Send(command);

                        return createCommandResult.Match(
                            createCommandResult =>
                            {
                                var eventResponse = mapper.Map<EventResponse>(createCommandResult);
                                return Results.Ok(eventResponse);
                            },
                            error => error.Result());
                    })
                .WithName("Create an event")
                .RequireAuthorization("IsAdmin")
                .DisableAntiforgery();
            
            endpoints.MapGet("/asso-events",
                    async (IMediator mediator, IMapper mapper) =>
                    {
                        var query = new GetAllAssoEventsQuery();
                        var getAllEventsResult = await mediator.Send(query);

                        return getAllEventsResult.Match(
                            getAllEventsResult =>
                            {
                                var eventResponse = mapper.Map<List<EventResponse>>(getAllEventsResult);
                                return Results.Ok(eventResponse);
                            },
                            error => error.Result());
                    })
                .WithName("Get all events");
            
            endpoints.MapGet("/asso-events/next-bingo",
                    async (IMediator mediator, IMapper mapper) =>
                    {
                        var query = new GetNextBingoQuery();
                        var getAllEventsResult = await mediator.Send(query);

                        return getAllEventsResult.Match(
                            getAllEventsResult =>
                            {
                                var eventResponse = mapper.Map<EventResponse>(getAllEventsResult);
                                return Results.Ok(eventResponse);
                            },
                            error => error.Result());
                    })
                .WithName("Get next bingo event");
            
            endpoints.MapGet("/asso-events/next-books",
                    async (IMediator mediator, IMapper mapper) =>
                    {
                        var query = new GetNextBooksQuery();
                        var getAllEventsResult = await mediator.Send(query);

                        return getAllEventsResult.Match(
                            getAllEventsResult =>
                            {
                                var eventResponse = mapper.Map<EventResponse>(getAllEventsResult);
                                return Results.Ok(eventResponse);
                            },
                            error => error.Result());
                    })
                .WithName("Get next books event");
            
            endpoints.MapGet("/asso-events/next-other-event",
                    async (IMediator mediator, IMapper mapper) =>
                    {
                        var query = new GetNextOtherEventQuery();
                        var getAllEventsResult = await mediator.Send(query);

                        return getAllEventsResult.Match(
                            getAllEventsResult =>
                            {
                                var eventResponse = mapper.Map<List<EventResponse>>(getAllEventsResult);
                                return Results.Ok(eventResponse);
                            },
                            error => error.Result());
                    })
                .WithName("Get next other event");
            
            endpoints.MapGet("/asso-events/{id}",
                    async (Guid id,
                        IMediator mediator, IMapper mapper) =>
                    {
                        var command = new GetAssoEventByIdQuery(
                            new AssoEventsId(id));
                        
                        var assoEventResult = await mediator.Send(command);

                        return assoEventResult.Match(
                            assoEventResult =>
                            {
                                var eventResponse = mapper.Map<EventResponse>(assoEventResult);
                                return Results.Ok(eventResponse);
                            },
                            error => error.Result());
                    })
                .WithName("Get AssoEvent By Id");
            
            endpoints.MapDelete("/asso-events/{id}",
                    async (Guid id,
                        IMediator mediator, IMapper mapper) =>
                    {
                        var command = new DeleteEventCommand(
                            new AssoEventsId(id));
                        
                        var assoEventResult = await mediator.Send(command);

                        return assoEventResult.Match(
                            assoEventResult =>
                            {
                                if (assoEventResult)
                                {
                                    return Results.Ok();
                                }
                                else
                                {
                                    return Results.NotFound();
                                }
                            },
                            error => error.Result());
                    })
                .WithName("Delete AssoEvent By Id");
            
            endpoints.MapPut("/asso-events/{id}",
                    async (Guid id, [FromForm] UpdateEventRequest request,
                        IMediator mediator, IMapper mapper) =>
                    {
                        var command = mapper.Map<UpdateEventCommand>((request, id));
                        
                        var assoEventResult = await mediator.Send(command);

                        return assoEventResult.Match(
                            assoEventResult =>
                            {
                                    var res = mapper.Map<EventResponse>(assoEventResult);
                                    return Results.Ok(res);
                            },
                            error => error.Result());
                    })
                .WithName("Update AssoEvent")
                .RequireAuthorization("IsAdmin")
                .DisableAntiforgery();
            
            endpoints.MapPost("/asso-events/{id}/numeros",
                    async (AddNumeroToPartieRequest request, 
                        Guid id,
                        IMediator mediator, IMapper mapper, ISSEClientManager sseClientManager) =>
                    {
                        var command = new AddNumeroToEventCommand(
                            new AssoEventsId(id),
                            request.Numero ?? 0);
                        
                        var addNumeroResult = await mediator.Send(command);

                        return addNumeroResult.Match(
                            addNumeroResult =>
                            {
                                var eventResponse = mapper.Map<EventResponse>(addNumeroResult);
                                
                                var jsonMessage = JsonSerializerHelper.Serialize(eventResponse);
                                sseClientManager.SendToAllClients(jsonMessage);
                                
                                return Results.Ok(eventResponse);
                            },
                            error => error.Result());
                    })
                .WithName("Add a number to a partie")
                .RequireAuthorization("IsAdmin");
            
            endpoints.MapDelete("/asso-events/{id}/numeros",
                    async (Guid id,
                        IMediator mediator, IMapper mapper, ISSEClientManager sseClientManager) =>
                    {
                        var command = new RemoveLastNumeroCommand(
                            new AssoEventsId(id));
                        
                        var removeLastNumeroResult = await mediator.Send(command);

                        return removeLastNumeroResult.Match(
                            removeLastNumeroResult =>
                            {
                                var eventResponse = mapper.Map<EventResponse>(removeLastNumeroResult);
                                
                                var jsonMessage = JsonSerializerHelper.Serialize(eventResponse);
                                sseClientManager.SendToAllClients(jsonMessage);
                                
                                return Results.Ok(eventResponse);
                            },
                            error => error.Result());
                    })
                .WithName("Remove last numero of a partie")
                .RequireAuthorization("IsAdmin");
            
            endpoints.MapPost("/asso-events/{id}/win-partie",
                    async (Guid id,
                        IMediator mediator, IMapper mapper, ISSEClientManager sseClientManager) =>
                    {
                        var command = new AddWinPartieCommand(
                            new AssoEventsId(id));
                                    
                        var createPartieResult = await mediator.Send(command);
            
                        return createPartieResult.Match(
                            createPartieResult =>
                            {
                                var eventResponse = mapper.Map<EventResponse>(createPartieResult);
                                
                                var  jsonMessage = JsonSerializerHelper.Serialize(eventResponse);
                                sseClientManager.SendToAllClients(jsonMessage);
                                
                                return Results.Ok(eventResponse);
                            },
                            error => error.Result());
                    })
                .WithName("Add win to a partie")
                .RequireAuthorization("IsAdmin");
            
            endpoints.MapPut("/asso-events/{id}/bingo-win",
                    async (AddBingoWinRequest request, Guid id,
                        IMediator mediator, IMapper mapper, ISSEClientManager sseClientManager) =>
                    {
                        var command = new AddBingoWinCommand(
                            new AssoEventsId(id), request.HasBeenWon!.Value);
                                    
                        var createPartieResult = await mediator.Send(command);
            
                        return createPartieResult.Match(
                            createPartieResult =>
                            {
                                var eventResponse = mapper.Map<EventResponse>(createPartieResult);
                                
                                var jsonMessage = JsonSerializerHelper.Serialize(eventResponse);
                                sseClientManager.SendToAllClients(jsonMessage);
                                
                                return Results.Ok(eventResponse);
                            },
                            error => error.Result());
                    })
                .WithName("Update Bingo win")
                .RequireAuthorization("IsAdmin");
            
            endpoints.MapGet("/asso-events/{id}/tableau/sse",
                    async (HttpContext ctx, Guid id, 
                        IMediator mediator, IMapper mapper,
                        ISSEClientManager sseClientManager, CancellationToken ct) =>
                    {
                        ctx.Response.Headers.Append("Content-Type", "text/event-stream");
                        ctx.Response.Headers.Append("Cache-Control", "no-cache");
    
                        var clientId = ctx.Connection.Id;
                        var streamWriter = new StreamWriter(ctx.Response.Body);

                        sseClientManager.AddClient(clientId, streamWriter);
                        
                        var command = new GetAssoEventByIdQuery(
                            new AssoEventsId(id));
                        
                        var assoEventResult = await mediator.Send(command, ct);

                        await assoEventResult.MatchAsync(
                            async assoEventResult =>
                            {
                                var eventResponse = mapper.Map<EventResponse>(assoEventResult);
                                await sseClientManager.SendToClient(clientId, JsonSerializerHelper.Serialize(eventResponse));
                                return Task.CompletedTask;
                            },
                            async error =>
                            {
                                return Task.CompletedTask;
                            });

                        try
                        {
                            // Keep the connection open
                            while (!ctx.RequestAborted.IsCancellationRequested)
                            {
                                await Task.Delay(100, ct);
                            }
                        }
                        catch (TaskCanceledException e)
                        {
                            await sseClientManager.SendToClient(clientId, $"data: {e.Message}\n\n");
                        }
                        catch (Exception e)
                        {
                            await sseClientManager.SendToClient(clientId, $"data: {e.Message}\n\n");
                        }
                        finally
                        {
                            sseClientManager.RemoveClient(clientId);
                        }
                    })
                .WithName("Get SSE for a tableau");
        });
    }
}