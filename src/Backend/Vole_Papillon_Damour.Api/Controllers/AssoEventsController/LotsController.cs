using MapsterMapper;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using Vole_Papillon_Damour.Api.Errors;
using Vole_Papillon_Damour.Application.Events.Commands.CreateEvent;
using Vole_Papillon_Damour.Application.Events.Commands.Lots.AddLotToPartieLine;
using Vole_Papillon_Damour.Application.Events.Commands.Lots.CreateLot;
using Vole_Papillon_Damour.Application.Events.Commands.Lots.DeleteLotFromPartieLine;
using Vole_Papillon_Damour.Application.Events.Commands.Lots.UpdateLot;
using Vole_Papillon_Damour.Contracts.Events.Requests.Lots;
using Vole_Papillon_Damour.Contracts.Events.Responses;
using Vole_Papillon_Damour.Domain.AssoEventsAggregate.ValueObjects;
using Vole_Papillon_Damour.Domain.EventsAggregate.ValueObjects;

namespace Vole_Papillon_Damour.Api.Controllers.AssoEventsController;

public static class LotsController
{
    public static IApplicationBuilder UseLotsController(this IApplicationBuilder builder)
    {
        return builder.UseEndpoints(endpoints =>
        {
            endpoints.MapPost("/asso-events/{assoId}/parties/{partieId}/partie-lines/{partieLineId}/lots",
                    async ([FromForm]AddLotToPartieLineRequest request, Guid assoId, Guid partieId, Guid partieLineId,
                        IMediator mediator, IMapper mapper) =>
                    {
                        var commandCreateLot = mapper.Map<CreateLotsCommand>(request);
                        var command = new AddLotToPartieLineCommand()
                        {
                            AssoEventsId = AssoEventsId.Create(assoId),
                            PartieId = PartieId.Create(partieId),
                            LinePartieId = LinePartieId.Create(partieLineId),
                            LotsCommand = commandCreateLot
                        };
                                    
                        var addLotToPartieLineResult = await mediator.Send(command);
            
                        return addLotToPartieLineResult.Match(
                            addLotToPartieLineResult =>
                            {
                                var eventResponse = mapper.Map<EventResponse>(addLotToPartieLineResult);
                                return Results.Ok(eventResponse);
                            },
                            error => error.Result());
                    })
                .WithName("Add Lot to PartieLine")
                .DisableAntiforgery()
                .RequireAuthorization("IsAdmin")
                .WithOpenApi();
            
            endpoints.MapDelete("/asso-events/{assoId}/parties/{partieId}/partie-lines/{partieLineId}/lots/{lotId}",
                    async (Guid assoId, Guid partieId, Guid partieLineId, Guid lotId,
                        IMediator mediator, IMapper mapper) =>
                    {
                        var command = new DeleteLotFromPartieLineCommand()
                        {
                            AssoEventsId = AssoEventsId.Create(assoId),
                            PartieId = PartieId.Create(partieId),
                            LinePartieId = LinePartieId.Create(partieLineId),
                            LotId = LotId.Create(lotId)
                        };
                                    
                        var deleteLotFromPartieLineResult = await mediator.Send(command);
            
                        return deleteLotFromPartieLineResult.Match(
                            deleteLotFromPartieLineResult =>
                            {
                                var eventResponse = mapper.Map<EventResponse>(deleteLotFromPartieLineResult);
                                return Results.Ok(eventResponse);
                            },
                            error => error.Result());
                    })
                .WithName("Remove Lot from PartieLine")
                .RequireAuthorization("IsAdmin")
                .WithOpenApi();
            
            endpoints.MapPut("/asso-events/{assoId}/parties/{partieId}/partie-lines/{partieLineId}/lots/{lotId}",
                    async ([FromForm] UpdateLotRequest request, Guid assoId, Guid partieId, Guid partieLineId, Guid lotId,
                        IMediator mediator, IMapper mapper) =>
                    {
                        var command = mapper.Map<UpdateLotCommand>((assoId, partieId, partieLineId, lotId, request));
                                    
                        var deleteLotFromPartieLineResult = await mediator.Send(command);
            
                        return deleteLotFromPartieLineResult.Match(
                            deleteLotFromPartieLineResult =>
                            {
                                var eventResponse = mapper.Map<EventResponse>(deleteLotFromPartieLineResult);
                                return Results.Ok(eventResponse);
                            },
                            error => error.Result());
                    })
                .WithName("Update Lot")
                .DisableAntiforgery()
                .RequireAuthorization("IsAdmin")
                .WithOpenApi();
        });
    }
}