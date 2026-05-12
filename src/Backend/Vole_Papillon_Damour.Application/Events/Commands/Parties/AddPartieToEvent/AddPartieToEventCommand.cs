using ErrorOr;
using MediatR;
using Vole_Papillon_Damour.Application.Events.Commands.CreateEvent;
using Vole_Papillon_Damour.Application.Events.Common;
using Vole_Papillon_Damour.Domain.EventsAggregate.ValueObjects;

namespace Vole_Papillon_Damour.Application.Events.Commands.Parties.AddPartieToEvent;

public class AddPartieToEventCommand : IRequest<ErrorOr<AssoEventResult>>
{
    public AssoEventsId AssoEventsId { get; set; }
    public CreatePartiesCommand PartiesCommand { get; set; }
}