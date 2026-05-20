using Mapster;
using Vole_Papillon_Damour.Application.Authentication.Common;
using Vole_Papillon_Damour.Contracts.Authentication;
using Vole_Papillon_Damour.Contracts.Authentication.Responses;
using Vole_Papillon_Damour.Contracts.Product.Requests;
using Vole_Papillon_Damour.Contracts.Product.Responses;
using Vole_Papillon_Damour.Domain.ProductAggregate.ValueObjects;

namespace Vole_Papillon_Damour.Api.Common.Mapping;

public class AuthenticationMappingConfig : IRegister
{
    public void Register(TypeAdapterConfig config)
    {
        config.NewConfig<AuthenticationResult, AuthenticationResponse>()
            .Map(dest => dest.Token, src => src.Token)
            .Map(dest => dest.Id, src => src.User.Id.Value)
            .Map(dest => dest, src => src.User);
    }
}