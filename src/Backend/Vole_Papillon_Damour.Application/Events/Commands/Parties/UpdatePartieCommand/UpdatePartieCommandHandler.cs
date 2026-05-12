using ErrorOr;
using MapsterMapper;
using MediatR;
using Vole_Papillon_Damour.Application.Common.Interfaces.Persistence;
using Vole_Papillon_Damour.Application.Events.Common;
using Vole_Papillon_Damour.Domain.AssoEventsAggregate;
using Vole_Papillon_Damour.Domain.Common.Errors;

namespace Vole_Papillon_Damour.Application.Events.Commands.Events.Partie.UpdatePartieCommand;

public class UpdatePartieCommandHandler(IEventRepository eventRepository, IMapper mapper)
    : IRequestHandler<UpdatePartieCommand, ErrorOr<AssoEventResult>>
{
    public async Task<ErrorOr<AssoEventResult>> Handle(UpdatePartieCommand command, CancellationToken cancellationToken)
    {
        var assoEvent = await eventRepository.GetByIdAsync(command.AssoEventsId);
        if (assoEvent is null)
            return Errors.AssoEvent.AssoEventNotFound(command.AssoEventsId);

        var partie = assoEvent.Parties!.FirstOrDefault(p => p.Id == command.PartieId);
        if (partie is null)
            return Errors.AssoEvent.Partie.PartieNotFound(command.AssoEventsId, command.PartieId);

        partie.Update(command.Name, command.PartieType, command.PauseAfter);

        await eventRepository.UpdateAsync(assoEvent);

        assoEvent = await eventRepository.GetByIdAsync(command.AssoEventsId);
        
        return mapper.Map<AssoEventResult>(assoEvent!);
    }
}