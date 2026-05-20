using ErrorOr;
using MapsterMapper;
using MediatR;
using Vole_Papillon_Damour.Application.Common.Interfaces.Persistence;
using Vole_Papillon_Damour.Application.Events.Common;
using Vole_Papillon_Damour.Domain.AssoEventsAggregate.ValueObjects;
using Vole_Papillon_Damour.Domain.Common.Errors;

namespace Vole_Papillon_Damour.Application.Events.Commands.Events.AddWinPartie;

public class AddWinPartieCommandHandler(IEventRepository eventRepository, IMapper mapper)
    : IRequestHandler<AddWinPartieCommand, ErrorOr<AssoEventResult>>
{
    public async Task<ErrorOr<AssoEventResult>> Handle(AddWinPartieCommand command, CancellationToken cancellationToken)
    {
        var assoEvent = await eventRepository.GetByIdAsync(command.AssoEventsId);

        if (assoEvent is null)
        {
            return Errors.AssoEvent.AssoEventNotFound(command.AssoEventsId);
        }

        var parties = assoEvent.Parties?.ToList();
        if (parties is null ||
            assoEvent.CurrentPartieIndex < 0 ||
            assoEvent.CurrentPartieIndex >= parties.Count)
        {
            return mapper.Map<AssoEventResult>(assoEvent);
        } 
        
        var partie = parties.Find(p => p.Index == assoEvent.CurrentPartieIndex);
        if (partie is null)
        {
            return Errors.AssoEvent.Partie.PartieWithIndexNotFound(command.AssoEventsId, assoEvent.CurrentPartieIndex);
        }
        
        if (partie.AddWin())
        {
            assoEvent.CurrentPartieIndex++;
            var partieNext = parties.Find(p => p.Index == assoEvent.CurrentPartieIndex);
            if (partieNext?.PartieType.Value == PartieType.PartieTypeEnum.Bingo && assoEvent.BingoHasBeenWon)
            {
                assoEvent.CurrentPartieIndex++;
            }
        }

        // When on Bingo partie pass the bingo has been won
        if (partie.LastNumeros.Count != 0 && partie.PartieType.Value == PartieType.PartieTypeEnum.Bingo)
        {
            assoEvent.BingoHasBeenWon = true;
        }
        
        assoEvent = await eventRepository.UpdateAsync(assoEvent);
        return mapper.Map<AssoEventResult>(assoEvent);
    }
}