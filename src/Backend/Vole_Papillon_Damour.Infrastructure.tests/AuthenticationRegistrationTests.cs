using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using FluentAssertions;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Vole_Papillon_Damour.Infrastructure;

namespace Vole_Papillon_Damour.Infrastructure.tests;

public class AuthenticationRegistrationTests
{
    [Fact]
    public async Task AddInfrastructure_RegistersEntraAndLegacyBearerSchemes()
    {
        var services = new ServiceCollection();
        var configuration = new ConfigurationManager();
        configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["AzureAd:Instance"] = "https://example.ciamlogin.com/",
            ["AzureAd:TenantId"] = "tenant-id",
            ["AzureAd:ClientId"] = "api-client-id",
            ["AzureAd:Audience"] = "api-client-id",
            ["JwtSettings:Audience"] = "legacy-audience",
            ["JwtSettings:Issuer"] = "legacy-issuer",
            ["JwtSettings:Secret"] = "a-secret-long-enough-for-tests",
            ["JwtSettings:ExpiryMinutes"] = "60"
        });

        services.AddInfrastructure(configuration);

        using var provider = services.BuildServiceProvider();
        var schemes = await provider
            .GetRequiredService<IAuthenticationSchemeProvider>()
            .GetAllSchemesAsync();

        schemes
            .Select(scheme => scheme.Name)
            .Should()
            .Contain(["Bearer", "Entra", "LegacyJwt"]);

        var jwtOptions = provider.GetRequiredService<IOptionsMonitor<JwtBearerOptions>>();
        jwtOptions.Get("Entra").TokenValidationParameters.RoleClaimType.Should().Be("roles");
        jwtOptions.Get("LegacyJwt").TokenValidationParameters.RoleClaimType.Should().Be("role");
    }

    [Fact]
    public void AddInfrastructure_AcceptsV2EntraTokenUsingApiClientIdAsAudience()
    {
        var services = new ServiceCollection();
        var configuration = new ConfigurationManager();
        configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["AzureAd:Instance"] = "https://example.ciamlogin.com/",
            ["AzureAd:TenantId"] = "tenant-id",
            ["AzureAd:ClientId"] = "api-client-id",
            ["AzureAd:Audience"] = "api-client-id",
            ["JwtSettings:Audience"] = "legacy-audience",
            ["JwtSettings:Issuer"] = "legacy-issuer",
            ["JwtSettings:Secret"] = "a-secret-long-enough-for-tests",
            ["JwtSettings:ExpiryMinutes"] = "60"
        });

        services.AddInfrastructure(configuration);

        using var provider = services.BuildServiceProvider();
        var validationParameters = provider
            .GetRequiredService<IOptionsMonitor<JwtBearerOptions>>()
            .Get("Entra")
            .TokenValidationParameters;
        var token = new JwtSecurityToken(
            issuer: "https://example.ciamlogin.com/tenant-id/v2.0",
            audience: "api-client-id",
            claims: [new Claim("ver", "2.0")]);

        var act = () => validationParameters.AudienceValidator!(
            token.Audiences,
            token,
            validationParameters);

        act.Should().NotThrow();
    }
}
