using ErrorOr;
using MapsterMapper;
using MediatR;
using Vole_Papillon_Damour.Application.Common.Interfaces.Persistence;
using Vole_Papillon_Damour.Application.Common.Interfaces.Services;
using Vole_Papillon_Damour.Application.Events.Common;
using Vole_Papillon_Damour.Domain.AssoEventsAggregate;
using Vole_Papillon_Damour.Domain.AssoEventsAggregate.Entities;
using Vole_Papillon_Damour.Domain.AssoEventsAggregate.ValueObjects;
using Vole_Papillon_Damour.Domain.EventsAggregate.ValueObjects;

namespace Vole_Papillon_Damour.Application.Events.Commands.CreateEvent;

public class CreateEventCommandHandler(IEventRepository eventRepository, IBlobService blobService, IMapper mapper, IEmailService emailService)
    : IRequestHandler<CreateEventCommand, ErrorOr<AssoEventResult>>
{
    public async Task<ErrorOr<AssoEventResult>> Handle(CreateEventCommand command, CancellationToken cancellationToken)
    {
        //TODO Rollback when pb
        Uri? urlImagePrincipal = null;
        if(command.Image is not null)
            urlImagePrincipal = await blobService.UploadEventImagesAsync(command.Image.FileName, command.Image.OpenReadStream());
        
        Uri? urlImageMap = null;
        if(command.ImageMap is not null)
            urlImageMap = await blobService.UploadEventImagesAsync(command.ImageMap.FileName, command.ImageMap.OpenReadStream());
        
        var address = new Adresse(command.RoadNumber, command.City, command.Road, command.CityCode);
        
        var parties = command.Parties?.Select(async p =>
        {
            var lineParties = new List<LinePartie>();
            foreach (var linePartieCommand in p.LineParties!)
            {
                var lots = new List<Lot>();
                foreach (var lotsCommand in linePartieCommand.Lots)
                {
                    var urlImage = await blobService.UploadLotoImagesAsync(lotsCommand.ImageName, lotsCommand.ImageStream);
                    lots.Add(Lot.Create(lotsCommand.Name, urlImage.ToString(), lotsCommand.Index));
                }
                lineParties.Add(LinePartie.Create(lots, linePartieCommand.NumberLine));
            }

            return Partie.Create(
                p.Name,
                p.PartieType,
                p.Index,
                p.PauseAfter,
                lineParties);
        }).Select(t => t.Result).ToList() ?? [];
        
        var events = AssoEvents.Create(
            command.Name,
            urlImagePrincipal,
            command.EventType,
            command.DateStart,
            command.DateEnd,
            command.HourOpenDoors,
            command.HourCloseDoors,
            urlImageMap,
            address,
            command.UrlRegistration,
            parties,
            command.Description
        );
        
        /*
        await emailService.SendEmailToMailingListAsync("Nouvel événement", 
            $"<h1>{events.Name}</h1><p>{events.Description}</p>", cancellationToken);
        */
        return mapper.Map<AssoEventResult>(await eventRepository.AddAsync(events));
    }
}