using ErrorOr;
using MapsterMapper;
using MediatR;
using Vole_Papillon_Damour.Application.Common.Interfaces.Persistence;
using Vole_Papillon_Damour.Application.Events.Common;
using Vole_Papillon_Damour.Domain.AssoEventsAggregate;
using Vole_Papillon_Damour.Domain.Common.Errors;

namespace Vole_Papillon_Damour.Application.Events.Commands.Parties.ChangeIndexPartie;

public class ChangeIndexPartieCommandHandler(IEventRepository eventRepository, IMapper mapper)
    : IRequestHandler<ChangeIndexPartieCommand, ErrorOr<AssoEventResult>>
{
    public async Task<ErrorOr<AssoEventResult>> Handle(ChangeIndexPartieCommand command, CancellationToken cancellationToken)
    {
        var assoEvent = await eventRepository.GetByIdAsync(command.AssoEventsId);

        if (assoEvent is null)
        {
            return Errors.AssoEvent.AssoEventNotFound(command.AssoEventsId);
        }

        var partie = assoEvent.Parties?.ToList().Find(p => p.Id == command.PartieId);

        if (partie is null)
        {
            return Errors.AssoEvent.Partie.PartieNotFound(command.AssoEventsId, command.PartieId);
        }

        assoEvent.DeletePartie(partie);
        partie.Index = command.Index;
        assoEvent.AddPartie(partie);
        
        assoEvent = await eventRepository.UpdateAsync(assoEvent);
        return mapper.Map<AssoEvents, AssoEventResult>(assoEvent);
    }
}