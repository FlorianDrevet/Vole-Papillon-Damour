using System.Text.Json;
using MapsterMapper;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using Vole_Papillon_Damour.Api.Common.Utils;
using Vole_Papillon_Damour.Api.Errors;
using Vole_Papillon_Damour.Application.Actuality.Commands.DeleteActuality;
using Vole_Papillon_Damour.Application.BingoCard.Commands.AnalyzeBingoCard;
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
using Vole_Papillon_Damour.Contracts.Events.Requests.BingoCard;
using Vole_Papillon_Damour.Contracts.Events.Responses;
using Vole_Papillon_Damour.Domain.EventsAggregate.ValueObjects;
using Vole_Papillon_Damour.Infrastructure.Services;

namespace Vole_Papillon_Damour.Api.Controllers.AssoEventsController;

public static class BingoCardController
{
    public static IApplicationBuilder UseBingoCardController(this IApplicationBuilder builder)
    {
        return builder.UseEndpoints(endpoints =>
        {
            endpoints.MapPost("/bingo-card",
                    async ([FromForm] BingoCardRequest request, IMediator mediator, IMapper mapper) =>
                    {
                        var command = mapper.Map<AnalyzeBingoCardCommand>(request);
                        var createCommandResult = await mediator.Send(command);

                        return createCommandResult.Match(
                            createCommandResult =>
                            {
                                var eventResponse = mapper.Map<List<BingoCardResponse>>(createCommandResult);
                                return Results.Ok(eventResponse);
                            },
                            error => error.Result());
                    })
                .WithName("Get bingo card from picture")
                .DisableAntiforgery();
        });
    }
}