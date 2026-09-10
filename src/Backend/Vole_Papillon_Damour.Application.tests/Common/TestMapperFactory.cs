using Mapster;
using MapsterMapper;
using Vole_Papillon_Damour.Application.Actuality.Common;
using ActualityAggregate = Vole_Papillon_Damour.Domain.ActualityAggregate.Actuality;

namespace Vole_Papillon_Damour.Application.tests.Common;

internal static class TestMapperFactory
{
    public static IMapper Create()
    {
        var config = new TypeAdapterConfig();
        config.NewConfig<ActualityAggregate, ActualityResult>()
            .Map(destination => destination.Id, source => source.Id.Value);
        return new Mapper(config);
    }
}
