using ErrorOr;
using MapsterMapper;
using MediatR;
using Vole_Papillon_Damour.Application.Common.Interfaces.Persistence;
using Vole_Papillon_Damour.Application.Common.Interfaces.Services;
using Vole_Papillon_Damour.Application.Events.Common;
using Vole_Papillon_Damour.Domain.Common.Errors;

namespace Vole_Papillon_Damour.Application.Events.Commands.Lots.UpdateLot;

public class UpdateLotCommandHandler(IEventRepository eventRepository, IBlobService blobService, IMapper mapper)
    : IRequestHandler<UpdateLotCommand, ErrorOr<AssoEventResult>>
{
    public async Task<ErrorOr<AssoEventResult>> Handle(UpdateLotCommand command, CancellationToken cancellationToken)
    {
        var assoEvent = await eventRepository.GetByIdAsync(command.AssoEventsId);
        if (assoEvent is null)
            return Errors.AssoEvent.AssoEventNotFound(command.AssoEventsId);

        var partie = assoEvent.Parties!.FirstOrDefault(p => p.Id == command.PartieId);
        if (partie is null)
            return Errors.AssoEvent.Partie.PartieNotFound(command.AssoEventsId, command.PartieId);

        var linePartie = partie.LineParties!.FirstOrDefault(l => l.Id == command.LinePartieId);
        if (linePartie is null)
            return Errors.AssoEvent.Partie.LinePartie.PartieLineNotFound(command.AssoEventsId, command.PartieId, command.LinePartieId);

        var lot = linePartie.Lots!.FirstOrDefault(l => l.Id == command.LotId);
        if (lot is null)
            return Errors.AssoEvent.Partie.LinePartie.Lot.LotNotFound(command.AssoEventsId, command.PartieId, command.LinePartieId, command.LotId);

        var urlImage = command.ImageUri;
        if (urlImage is null)
        {
            urlImage =  await blobService.UploadLotoImagesAsync(command.Image!.FileName, command.Image!.OpenReadStream());
        }
        
        lot.Update(command.Name, urlImage!.ToString());

        await eventRepository.UpdateAsync(assoEvent);
        
        return mapper.Map<AssoEventResult>(assoEvent!);
    }
}