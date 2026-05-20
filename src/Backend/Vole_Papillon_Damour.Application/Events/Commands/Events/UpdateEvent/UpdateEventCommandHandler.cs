using ErrorOr;
using MapsterMapper;
using MediatR;
using Vole_Papillon_Damour.Application.Actuality.Common;
using Vole_Papillon_Damour.Application.Common.Interfaces.Persistence;
using Vole_Papillon_Damour.Application.Common.Interfaces.Services;
using Vole_Papillon_Damour.Application.Events.Common;
using Vole_Papillon_Damour.Domain.Common.Errors;

namespace Vole_Papillon_Damour.Application.Events.Commands.Events.UpdateEvent;

public class UpdateEventCommandHandler(IEventRepository eventRepository, IBlobService blobService, IMapper mapper)
    : IRequestHandler<UpdateEventCommand, ErrorOr<AssoEventResult>>
{
    public async Task<ErrorOr<AssoEventResult>> Handle(UpdateEventCommand command, CancellationToken cancellationToken)
    {
        var vpdEvent = await eventRepository.GetByIdAsync(command.Id);

        if (vpdEvent is null)
        {
            return Errors.AssoEvent.AssoEventNotFound(command.Id);
        }

        var urlImagePrincipal = command.ImageUri;
        if (urlImagePrincipal is null)
        {
            urlImagePrincipal = await blobService.UploadEventImagesAsync(command.Image!.FileName,
                command.Image!.OpenReadStream());
        }
        
        var urlImageMap = command.ImageMapUri;
        if (urlImageMap is null && command.ImageMap is not null)
        {
            urlImageMap = await blobService.UploadEventImagesAsync(command.ImageMap!.FileName,
                command.ImageMap!.OpenReadStream());
        }
        
        vpdEvent.Update(
            command.Name,
            urlImagePrincipal,
            command.EventType,
            command.DateStart,
            command.DateEnd,
            command.HourOpenDoors,
            command.HourCloseDoors,
            urlImageMap,
            command.Adresse,
            command.Description,
            command.UrlRegistration
        );

        vpdEvent = await eventRepository.UpdateAsync(vpdEvent);
        return mapper.Map<AssoEventResult>(vpdEvent);
 
    }
}