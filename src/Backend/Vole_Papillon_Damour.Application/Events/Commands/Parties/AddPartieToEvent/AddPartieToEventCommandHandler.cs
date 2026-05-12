using ErrorOr;
using MapsterMapper;
using MediatR;
using Vole_Papillon_Damour.Application.Common.Interfaces.Persistence;
using Vole_Papillon_Damour.Application.Common.Interfaces.Services;
using Vole_Papillon_Damour.Application.Events.Common;
using Vole_Papillon_Damour.Domain.AssoEventsAggregate.Entities;
using Vole_Papillon_Damour.Domain.AssoEventsAggregate.ValueObjects;
using Vole_Papillon_Damour.Domain.Common.Errors;
using Vole_Papillon_Damour.Domain.EventsAggregate.ValueObjects;

namespace Vole_Papillon_Damour.Application.Events.Commands.Parties.AddPartieToEvent;

public class AddPartieToEventCommandHandler(IEventRepository eventRepository, IMapper mapper, IBlobService blobService)
    : IRequestHandler<AddPartieToEventCommand, ErrorOr<AssoEventResult>>
{
    public async Task<ErrorOr<AssoEventResult>> Handle(AddPartieToEventCommand command, CancellationToken cancellationToken)
    {
        var assoEvent = await eventRepository.GetByIdAsync(command.AssoEventsId);

        if (assoEvent is null)
        {
            return Errors.AssoEvent.AssoEventNotFound(command.AssoEventsId);
        }

        var partieCommand = command.PartiesCommand;

        var lineParties = partieCommand.LineParties?.Select(x =>
        {
            var lots = x.Lots.Select(async l =>
            {
                var urlImage = await blobService.UploadLotoImagesAsync(l.ImageName, l.ImageStream);
                return Lot.Create(l.Name, urlImage.ToString(), l.Index);
            }).Select(x => x.Result).ToList();
            return LinePartie.Create(lots, x.NumberLine);
        }).ToList() ?? [];

        var partie = Partie.Create(partieCommand.Name,
            partieCommand.PartieType,
            partieCommand.Index,
            partieCommand.PauseAfter,
            lineParties);
        
        assoEvent.AddPartie(partie);
        
        // TODO understand why update not working
        // TODO create methode update
        await eventRepository.DeleteAsync(assoEvent.Id);
        assoEvent = await eventRepository.AddAsync(assoEvent);

        return mapper.Map<AssoEventResult>(assoEvent);
    }
}