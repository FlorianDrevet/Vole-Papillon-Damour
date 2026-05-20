using ErrorOr;
using MediatR;
using Microsoft.AspNetCore.Http;
using Vole_Papillon_Damour.Application.Events.Common;
using Vole_Papillon_Damour.Domain.AssoEventsAggregate;
using Vole_Papillon_Damour.Domain.AssoEventsAggregate.ValueObjects;
using Vole_Papillon_Damour.Domain.EventsAggregate.ValueObjects;

namespace Vole_Papillon_Damour.Application.Events.Commands.CreateEvent;

public record CreateEventCommand(
    string Name,
    IFormFile? Image,
    EventsType EventType,
    DateTimeOffset DateStart,
    DateTimeOffset? DateEnd,
    DateTimeOffset? HourOpenDoors,
    DateTimeOffset? HourCloseDoors,
    Uri? UrlRegistration,
    IFormFile? ImageMap,
    string Description,
    int? RoadNumber, 
    string City,
    int CityCode,
    string Road,
    List<CreatePartiesCommand>? Parties) : IRequest<ErrorOr<AssoEventResult>>;

public class CreatePartiesCommand
{
    public string? Name { get; init; }
    public PartieType PartieType { get; init; }
    public bool PauseAfter { get; init; }
    public int Index { get; init; }
    public List<CreateLinePartiCommand>? LineParties { get; init; }
}

public class CreateLinePartiCommand
{
    public List<CreateLotsCommand> Lots { get; set; } = null!;
    public NumberLine NumberLine { get; set; } = null!;
    public int Index { get; set; }
}


public class CreateLotsCommand
{
    public string Name { get; init; }
    public Stream ImageStream { get; init; }
    public string ImageName { get; init; }
    public NumberLine NumberLine { get; init; }
    public int Index { get; init; }
}