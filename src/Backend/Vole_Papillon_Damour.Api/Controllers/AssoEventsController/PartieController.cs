using Azure.Core;
using MapsterMapper;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using Vole_Papillon_Damour.Api.Errors;
using Vole_Papillon_Damour.Application.Events.Commands.CreateEvent;
using Vole_Papillon_Damour.Application.Events.Commands.Events.Partie.UpdatePartieCommand;
using Vole_Papillon_Damour.Application.Events.Commands.Parties.AddPartieToEvent;
using Vole_Papillon_Damour.Application.Events.Commands.Parties.ChangeIndexPartie;
using Vole_Papillon_Damour.Application.Events.Commands.Parties.DeletePartie;
using Vole_Papillon_Damour.Contracts.Events.Requests;
using Vole_Papillon_Damour.Contracts.Events.Requests.Parties;
using Vole_Papillon_Damour.Contracts.Events.Responses;
using Vole_Papillon_Damour.Domain.AssoEventsAggregate.ValueObjects;
using Vole_Papillon_Damour.Domain.EventsAggregate.ValueObjects;

namespace Vole_Papillon_Damour.Api.Controllers.AssoEventsController;

public static class PartieController
{
    public static IApplicationBuilder UsePartieController(this IApplicationBuilder builder)
    {
        return builder.UseEndpoints(endpoints =>
        {
            endpoints.MapDelete("/asso-events/{id}/parties/{partieId}",
                    async (Guid id,
                        Guid partieId,
                        IMediator mediator, IMapper mapper) =>
                    {
                        var command = new DeletePartieCommand(
                            new AssoEventsId(id),
                            new PartieId(partieId));
                                    
                        var removePartieResult = await mediator.Send(command);
            
                        return removePartieResult.Match(
                            removePartieResult =>
                            {
                                var eventResponse = mapper.Map<EventResponse>(removePartieResult);
                                return Results.Ok(eventResponse);
                            },
                            error => error.Result());
                    })
                .WithName("Remove partie of an event")
                .RequireAuthorization("IsAdmin");
            
            
            endpoints.MapPut("/asso-events/{id:guid}/parties/{partieId:guid}/index/{index:int}",
                    async (Guid id,
                        Guid partieId,
                        int index,
                        IMediator mediator, IMapper mapper) =>
                    {
                        var command = new ChangeIndexPartieCommand(
                            new AssoEventsId(id),
                            new PartieId(partieId),
                            index);
                                    
                        var changeIndexResult = await mediator.Send(command);
            
                        return changeIndexResult.Match(
                            removePartieResult =>
                            {
                                var eventResponse = mapper.Map<EventResponse>(removePartieResult);
                                return Results.Ok(eventResponse);
                            },
                            error => error.Result());
                    })
                .WithName("Change Index of a partie")
                .RequireAuthorization("IsAdmin");
            
            endpoints.MapPost("/asso-events/{id:guid}/parties",
                    async ([FromForm] CreatePartiesRequest request,
                        Guid id,
                        IMediator mediator, IMapper mapper) =>
                    {
                        var command = new AddPartieToEventCommand();
                        command.PartiesCommand = mapper.Map<CreatePartiesCommand>(request);
                        command.AssoEventsId = AssoEventsId.Create(id);
                                    
                        var createPartieResult = await mediator.Send(command);
            
                        return createPartieResult.Match(
                            createPartieResult =>
                            {
                                var eventResponse = mapper.Map<EventResponse>(createPartieResult);
                                return Results.Ok(eventResponse);
                            },
                            error => error.Result());
                    })
                .WithName("Add partie of an event")
                .RequireAuthorization("IsAdmin")
                .DisableAntiforgery();
            
            endpoints.MapPut("/asso-events/{id:guid}/parties/{partieId:guid}",
                    async ([FromForm] UpdatePartieRequest request,
                        Guid id,
                        Guid partieId,
                        IMediator mediator, IMapper mapper) =>
                    {
                        var command =  mapper.Map<UpdatePartieCommand>((id, partieId, request));
                                    
                        var createPartieResult = await mediator.Send(command);
            
                        return createPartieResult.Match(
                            createPartieResult =>
                            {
                                var eventResponse = mapper.Map<EventResponse>(createPartieResult);
                                return Results.Ok(eventResponse);
                            },
                            error => error.Result());
                    })
                .WithName("Update partie of an event")
                .RequireAuthorization("IsAdmin")
                .DisableAntiforgery();
        });
    }
}