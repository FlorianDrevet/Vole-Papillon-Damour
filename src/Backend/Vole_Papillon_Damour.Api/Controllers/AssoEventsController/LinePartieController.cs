using MapsterMapper;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using Vole_Papillon_Damour.Api.Errors;
using Vole_Papillon_Damour.Application.Events.Commands.CreateEvent;
using Vole_Papillon_Damour.Application.Events.Commands.LineParties.AddLinePartieToPartie;
using Vole_Papillon_Damour.Application.Events.Commands.LineParties.DeleteLinePartieFromPartie;
using Vole_Papillon_Damour.Application.Events.Commands.Lots.AddLotToPartieLine;
using Vole_Papillon_Damour.Contracts.Events.Requests;
using Vole_Papillon_Damour.Contracts.Events.Responses;
using Vole_Papillon_Damour.Domain.AssoEventsAggregate.ValueObjects;
using Vole_Papillon_Damour.Domain.EventsAggregate.ValueObjects;

namespace Vole_Papillon_Damour.Api.Controllers.AssoEventsController;

public static class LinePartieController
{
        public static IApplicationBuilder UseLinePartieController(this IApplicationBuilder builder)
    {
        return builder.UseEndpoints(endpoints =>
        {
            endpoints.MapPost("/asso-events/{assoId:guid}/parties/{partieId:guid}/partie-lines",
                    async ([FromForm]AddPartieLineToPartieRequest request, Guid assoId, Guid partieId,
                        IMediator mediator, IMapper mapper) =>
                    {
                        var commandCreateLinePartie = mapper.Map<CreateLinePartiCommand>(request);
                        var command = new AddLinePartieToPartieCommand()
                        {
                            AssoEventsId = AssoEventsId.Create(assoId),
                            PartieId = PartieId.Create(partieId),
                            LinePartieCommand = commandCreateLinePartie
                        };
                                    
                        var addLinePartieToPartieResult = await mediator.Send(command);
            
                        return addLinePartieToPartieResult.Match(
                            addLinePartieToPartieResult =>
                            {
                                var eventResponse = mapper.Map<EventResponse>(addLinePartieToPartieResult);
                                return Results.Ok(eventResponse);
                            },
                            error => error.Result());
                    })
                .WithName("Add Line Partie to Partie")
                .DisableAntiforgery()
                .RequireAuthorization("IsAdmin");
            
            endpoints.MapDelete("/asso-events/{assoId:guid}/parties/{partieId:guid}/partie-lines/{linePartieId:guid}",
                    async (Guid assoId, Guid partieId, Guid linePartieId,
                        IMediator mediator, IMapper mapper) =>
                    {
                        var command = new DeleteLinePartieFromPartieCommand()
                        {
                            AssoEventsId = AssoEventsId.Create(assoId),
                            PartieId = PartieId.Create(partieId),
                            LinePartieId = LinePartieId.Create(linePartieId)
                        };
                                    
                        var deleteLinePartieFromPartieResult = await mediator.Send(command);
            
                        return deleteLinePartieFromPartieResult.Match(
                            deleteLinePartieFromPartieResult =>
                            {
                                var eventResponse = mapper.Map<EventResponse>(deleteLinePartieFromPartieResult);
                                return Results.Ok(eventResponse);
                            },
                            error => error.Result());
                    })
                .WithName("Remove Line Partie from Partie")
                .RequireAuthorization("IsAdmin");

        });
    }
}