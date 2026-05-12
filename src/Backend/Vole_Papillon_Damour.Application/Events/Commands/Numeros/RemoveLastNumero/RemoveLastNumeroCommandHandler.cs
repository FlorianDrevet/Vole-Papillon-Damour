using ErrorOr;
using MapsterMapper;
using MediatR;
using Vole_Papillon_Damour.Application.Common.Interfaces.Persistence;
using Vole_Papillon_Damour.Application.Events.Common;
using Vole_Papillon_Damour.Domain.AssoEventsAggregate.ValueObjects;
using Vole_Papillon_Damour.Domain.Common.Errors;
using Vole_Papillon_Damour.Domain.EventsAggregate.ValueObjects;

namespace Vole_Papillon_Damour.Application.Events.Commands.RemoveLastNumero;

public class RemoveLastNumeroCommandHandler(IEventRepository eventRepository, IMapper mapper)
    : IRequestHandler<RemoveLastNumeroCommand, ErrorOr<AssoEventResult>>
{
    public async Task<ErrorOr<AssoEventResult>> Handle(RemoveLastNumeroCommand command, CancellationToken cancellationToken)
    {
        var assoEvent = await eventRepository.GetByIdAsync(command.AssoEventsId);

        if (assoEvent is null)
        {
            return Errors.AssoEvent.AssoEventNotFound(command.AssoEventsId);
        }

        if (assoEvent.CurrentPartieIndex < 0 || 
            assoEvent.Parties is null ||
            assoEvent.CurrentPartieIndex >= assoEvent.Parties?.Count)
        {
            return mapper.Map<AssoEventResult>(assoEvent);
        }
        
        var partie = assoEvent.Parties?.ToList().Find(p => p.Index == assoEvent.CurrentPartieIndex);
        int? numeroRemoved = partie!.RemoveLastNumero();
        if (numeroRemoved is null)
        {
            if (assoEvent.CurrentPartieIndex > 0)
            {
                assoEvent.CurrentPartieIndex--;
                var partieBefore = assoEvent.Parties?.ToList().Find(p => p.Index == assoEvent.CurrentPartieIndex);
                partieBefore!.RemoveLastNumero();
            }
        }
        
        // If the numero removed is the one added by this partie to the bingo
        // We need to remove it from the bingo numerous
        if (numeroRemoved != null && numeroRemoved == partie.AddedBingoNumber)
        {
            partie.AddedBingoNumber = null;
            if (!assoEvent.RemoveBingoNumero(numeroRemoved!.Value))
            {
                return Errors.AssoEvent.CantRemoveBingoNumero(command.AssoEventsId, numeroRemoved.Value);
            }
        }

        assoEvent = await eventRepository.UpdateAsync(assoEvent);
        return mapper.Map<AssoEventResult>(assoEvent);
    }
}