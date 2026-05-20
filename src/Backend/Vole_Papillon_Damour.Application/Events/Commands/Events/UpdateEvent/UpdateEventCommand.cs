using ErrorOr;
using MediatR;
using Microsoft.AspNetCore.Http;
using Vole_Papillon_Damour.Application.Events.Common;
using Vole_Papillon_Damour.Domain.AssoEventsAggregate.ValueObjects;
using Vole_Papillon_Damour.Domain.EventsAggregate.ValueObjects;

namespace Vole_Papillon_Damour.Application.Events.Commands.Events.UpdateEvent;

public record UpdateEventCommand (
    AssoEventsId Id,
    string Name ,
    IFormFile? Image ,
    Uri? ImageUri ,
    EventsType EventType ,
    DateTimeOffset DateStart ,
    DateTimeOffset? DateEnd ,
    DateTimeOffset? HourOpenDoors ,
    DateTimeOffset? HourCloseDoors ,
    Uri? UrlRegistration ,
    IFormFile? ImageMap ,
    Uri? ImageMapUri ,
    string Description ,
    Adresse Adresse
) : IRequest<ErrorOr<AssoEventResult>>;