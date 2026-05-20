using System.Diagnostics.CodeAnalysis;
using ErrorOr;
using MapsterMapper;
using MediatR;
using Vole_Papillon_Damour.Application.Common.Interfaces.Persistence;
using Vole_Papillon_Damour.Application.Events.Common;
using Vole_Papillon_Damour.Domain.AssoEventsAggregate.ValueObjects;
using Vole_Papillon_Damour.Domain.Common.Errors;

namespace Vole_Papillon_Damour.Application.Events.Commands.Numeros.AddNumeroToEvent;

[SuppressMessage("ReSharper", "PossibleMultipleEnumeration")]
public class AddNumeroToEventCommandHandler(IEventRepository eventRepository, IMapper mapper)
    : IRequestHandler<AddNumeroToEventCommand, ErrorOr<AssoEventResult>>
{
    public async Task<ErrorOr<AssoEventResult>> Handle(AddNumeroToEventCommand command, CancellationToken cancellationToken)
    {
        var assoEvent = await eventRepository.GetByIdAsync(command.AssoEventsId);

        if (assoEvent is null)
        {
            return Errors.AssoEvent.AssoEventNotFound(command.AssoEventsId);
        }

        var partie = assoEvent.Parties?.ToList().Find(p => p.Index == assoEvent.CurrentPartieIndex);
        if (partie is null)
        {
            return Errors.AssoEvent.Partie.PartieWithIndexNotFound(command.AssoEventsId, assoEvent.CurrentPartieIndex);
        }

        if (!partie.AddLiveNumero(command.Numero))
        {
            return Errors.AssoEvent.Partie.NumeroAlreadyExists(command.AssoEventsId, partie.Id, command.Numero);
        }
        
        if (partie.AddedBingoNumber is null && partie.PartieType.Value != PartieType.PartieTypeEnum.Bingo)
        {
            if (assoEvent.AddBingoNumero(command.Numero))
            {
                partie.AddedBingoNumber = command.Numero;
            }
        }

        assoEvent = await eventRepository.UpdateAsync(assoEvent);
        return mapper.Map<AssoEventResult>(assoEvent);
    }
}