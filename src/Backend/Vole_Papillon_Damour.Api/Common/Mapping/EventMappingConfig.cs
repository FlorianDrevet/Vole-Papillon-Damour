using Azure.Core;
using Mapster;
using Vole_Papillon_Damour.Application.Events.Commands.CreateEvent;
using Vole_Papillon_Damour.Application.Events.Commands.Events.Partie.UpdatePartieCommand;
using Vole_Papillon_Damour.Application.Events.Commands.Events.UpdateEvent;
using Vole_Papillon_Damour.Application.Events.Commands.Lots.UpdateLot;
using Vole_Papillon_Damour.Application.Events.Commands.Parties.AddPartieToEvent;
using Vole_Papillon_Damour.Application.Events.Common;
using Vole_Papillon_Damour.Contracts.Events.Requests;
using Vole_Papillon_Damour.Contracts.Events.Requests.Lots;
using Vole_Papillon_Damour.Contracts.Events.Requests.Parties;
using Vole_Papillon_Damour.Contracts.Events.Responses;
using Vole_Papillon_Damour.Domain.AssoEventsAggregate;
using Vole_Papillon_Damour.Domain.AssoEventsAggregate.Entities;
using Vole_Papillon_Damour.Domain.AssoEventsAggregate.ValueObjects;
using Vole_Papillon_Damour.Domain.Common.Models;
using Vole_Papillon_Damour.Domain.EventsAggregate;
using Vole_Papillon_Damour.Domain.EventsAggregate.ValueObjects;

namespace Vole_Papillon_Damour.Api.Common.Mapping;

public class EventMappingConfig : IRegister
{
    public void Register(TypeAdapterConfig config)
    {
        TypeAdapterConfig<IFormFile, IFormFile>.ForType().MapWith(src => src);
        TypeAdapterConfig<IFormFile, Stream>.ForType().MapWith(src => src.OpenReadStream());
        
        TypeAdapterConfig<string, NumberLine>.ForType().MapWith(src => NumberLine.CreateFromString(src));
        TypeAdapterConfig<string, PartieType>.ForType().MapWith(src => PartieType.CreateFromString(src));
        TypeAdapterConfig<string, EventsType>.ForType().MapWith(src => EventsType.CreateFromString(src));
        
        TypeAdapterConfig<NumberLine, string>.ForType().MapWith(src => src.Value.ToString());
        TypeAdapterConfig<PartieType, string>.ForType().MapWith(src => src.Value.ToString());
        TypeAdapterConfig<EventsType, string>.ForType().MapWith(src => src.Value.ToString());
        
        TypeAdapterConfig<LotId, Guid>.ForType().MapWith(src => src.Value);
        TypeAdapterConfig<PartieId, Guid>.ForType().MapWith(src => src.Value);
        TypeAdapterConfig<LinePartieId, Guid>.ForType().MapWith(src => src.Value);
        TypeAdapterConfig<AssoEventsId, Guid>.ForType().MapWith(src => src.Value);
        
        TypeAdapterConfig<Guid, AssoEventsId>.ForType().MapWith(src => new AssoEventsId(src));
        TypeAdapterConfig<Guid, PartieId>.ForType().MapWith(src => new PartieId(src));
        TypeAdapterConfig<Guid, LinePartieId>.ForType().MapWith(src => new LinePartieId(src));
        TypeAdapterConfig<Guid, LotId>.ForType().MapWith(src => new LotId(src));
        
        // Event

        config.NewConfig<(UpdateEventRequest Request, Guid Id), UpdateEventCommand>()
            .Map(dest => dest.Id, src => src.Id)
            .Map(dest => dest.Adresse, src =>
                new Adresse(
                    src.Request.RoadNumber,
                    src.Request.City ?? string.Empty,
                    src.Request.Road ?? string.Empty,
                    src.Request.CityCode ?? 0))
            .Map(dest => dest, src => src.Request);

        config.NewConfig<AssoEventResult, EventResponse>()
            .Map(dest => dest.EventType, src => src.EventsType.Value.ToString())
            .Map(dest =>dest.Id, src => src.Id.Value)
            .Map(dest => dest.Road, src => src.Adresse.Road)
            .Map(dest => dest.City, src => src.Adresse.City)
            .Map(dest => dest.CityCode, src => src.Adresse.CityCode)
            .Map(dest => dest.RoadNumber, src => src.Adresse.RoadNumber);

        config.NewConfig<AddPartieToEventRequest, AddPartieToEventCommand>()
            .Map(dest => dest.PartiesCommand, src => src.Partie);

        // Parties
        config.NewConfig<CreatePartiesRequest, CreatePartiesCommand>()
            .Map(dest => dest.PartieType, src => PartieType.CreateFromString(src.PartieType));
        
        config.NewConfig<PartieResult, CreatePartiesResponse>()
            .Map(dest =>dest.Id, src => src.Id.Value)
            .Map(dest => dest.PartieType, src => src.PartieType.Value.ToString());

        // LineParties
        config.NewConfig<CreateLinePartieRequest, CreateLinePartieRequest>()
            .Map(dest => dest.NumberLine, src => EventsType.CreateFromString(src.NumberLine));
        
        // Lots
        config.NewConfig<CreateLotsRequest, CreateLotsCommand>()
            .Map(dest => dest.ImageName, src => src.Image!.FileName)
            .Map(dest => dest.ImageStream, src => src.Image);
        
        config.NewConfig<(Guid AssoEventId, Guid PartieId, UpdatePartieRequest Request), UpdatePartieCommand>()
            .Map(dest => dest.PartieId, src => src.PartieId)
            .Map(dest => dest.AssoEventsId, src => src.AssoEventId)
            .Map(dest => dest, src => src.Request);
        
        config.NewConfig<(Guid AssoEventId, Guid PartieId, Guid PartieLineId, Guid LotId, UpdateLotRequest Request), UpdateLotCommand>()
            .Map(dest => dest.PartieId, src => src.PartieId)
            .Map(dest => dest.AssoEventsId, src => src.AssoEventId)
            .Map(dest => dest.LinePartieId, src => src.PartieLineId)
            .Map(dest => dest.LotId, src => src.LotId)
            .Map(dest => dest, src => src.Request);
    }
}