using Mapster;
using MapsterMapper;

namespace Vole_Papillon_Damour.Application.tests.Common;

internal static class TestMapperFactory
{
    public static IMapper Create()
    {
        var config = new TypeAdapterConfig();
        return new Mapper(config);
    }
}
