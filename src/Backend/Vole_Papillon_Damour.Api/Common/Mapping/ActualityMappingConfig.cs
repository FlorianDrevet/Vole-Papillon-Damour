using Mapster;
using Vole_Papillon_Damour.Application.Actuality.Commands.UpdateActuality;
using Vole_Papillon_Damour.Application.Actuality.Common;
using Vole_Papillon_Damour.Contracts.Actuality.Requests;
using Vole_Papillon_Damour.Domain.ActualityAggregate;
using Vole_Papillon_Damour.Domain.ActualityAggregate.ValueObjects;

namespace Vole_Papillon_Damour.Api.Common.Mapping;

public class ActualityMappingConfig : IRegister
{
    public void Register(TypeAdapterConfig config)
    {
        TypeAdapterConfig<IFormFile, IFormFile>.ForType().MapWith(src => src);
        
        config.NewConfig<ActualityId, Guid>()
            .Map(dest => dest, src => src.Value);

        config.NewConfig<(UpdateActualityRequest request, Guid id), UpdateActualityCommand>()
            .Map(dest => dest.Id, src => new ActualityId(src.id))
            .Map(dest => dest, src => src.request);
        
        config.NewConfig<Actuality, ActualityResult>()
            .Map(dest => dest.Id, src => src.Id.Value)
            .Map(dest => dest.UrlPrincipalImage, src => src.UrlPrincipalImage);
    }
}