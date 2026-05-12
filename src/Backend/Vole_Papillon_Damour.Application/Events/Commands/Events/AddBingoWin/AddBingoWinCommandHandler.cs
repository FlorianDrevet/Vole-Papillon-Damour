using ErrorOr;
using MapsterMapper;
using MediatR;
using Vole_Papillon_Damour.Application.Common.Interfaces.Persistence;
using Vole_Papillon_Damour.Application.Events.Common;
using Vole_Papillon_Damour.Domain.Common.Errors;

namespace Vole_Papillon_Damour.Application.Events.Commands.Events.AddBingoWin;

public class AddBingoWinCommandHandler(IEventRepository eventRepository, IMapper mapper)
    : IRequestHandler<AddBingoWinCommand, ErrorOr<AssoEventResult>>
{
    public async Task<ErrorOr<AssoEventResult>> Handle(AddBingoWinCommand command, CancellationToken cancellationToken)
    {
        var assoEvent = await eventRepository.GetByIdAsync(command.AssoEventsId);

        if (assoEvent is null)
        {
            return Errors.AssoEvent.AssoEventNotFound(command.AssoEventsId);
        }

        if (assoEvent.Parties is null ||
            assoEvent.CurrentPartieIndex >= assoEvent.Parties?.Count)
        {
            return mapper.Map<AssoEventResult>(assoEvent);
        } 
        
        assoEvent.BingoHasBeenWon = command.HasBeenWon;
        assoEvent = await eventRepository.UpdateAsync(assoEvent);
        return mapper.Map<AssoEventResult>(assoEvent);
    }
}