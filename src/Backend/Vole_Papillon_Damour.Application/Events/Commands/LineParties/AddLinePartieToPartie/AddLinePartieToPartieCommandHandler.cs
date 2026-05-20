using ErrorOr;
using MapsterMapper;
using MediatR;
using Vole_Papillon_Damour.Application.Common.Interfaces.Persistence;
using Vole_Papillon_Damour.Application.Common.Interfaces.Services;
using Vole_Papillon_Damour.Application.Events.Common;
using Vole_Papillon_Damour.Domain.AssoEventsAggregate.Entities;
using Vole_Papillon_Damour.Domain.AssoEventsAggregate.ValueObjects;
using Vole_Papillon_Damour.Domain.Common.Errors;

namespace Vole_Papillon_Damour.Application.Events.Commands.LineParties.AddLinePartieToPartie;

public class AddLinePartieToPartieCommandHandler(IEventRepository eventRepository, IBlobService blobService, IMapper mapper)
    : IRequestHandler<AddLinePartieToPartieCommand, ErrorOr<AssoEventResult>>
{
    public async Task<ErrorOr<AssoEventResult>> Handle(AddLinePartieToPartieCommand command, CancellationToken cancellationToken)
    {
        var assoEvent = await eventRepository.GetByIdAsync(command.AssoEventsId);
        if (assoEvent is null)
        {
            return Errors.AssoEvent.AssoEventNotFound(command.AssoEventsId);
        }

        var parties = assoEvent.Parties?.ToList().Find(p => p.Id == command.PartieId);
        if (parties is null)
        {
            return Errors.AssoEvent.Partie.PartieNotFound(command.AssoEventsId, command.PartieId);
        }

        var lots = command.LinePartieCommand.Lots.Select(async l =>
        {
            var urlImage = await blobService.UploadLotoImagesAsync(l.ImageName, l.ImageStream);
            return Lot.Create(l.Name, urlImage.ToString(), l.Index);
        }).Select(x => x.Result).ToList();
        
        int? index = parties.PartieType.Value == PartieType.PartieTypeEnum.Standard ? null : 0;
        var linePartie = LinePartie.Create(lots, command.LinePartieCommand.NumberLine, index);

        parties.AddLinePartie(linePartie);

        await eventRepository.UpdateAsync(assoEvent);
        return mapper.Map<AssoEventResult>(assoEvent);
    }
}