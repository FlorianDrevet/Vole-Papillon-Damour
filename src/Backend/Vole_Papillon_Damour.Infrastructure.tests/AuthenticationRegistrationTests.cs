using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using FluentAssertions;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Vole_Papillon_Damour.Infrastructure;

namespace Vole_Papillon_Damour.Infrastructure.tests;

public class AuthenticationRegistrationTests
{
    [Fact]
    public async Task AddInfrastructure_ConfiguresKeycloakForTheLocalProvider()
    {
        const string authority = "http://localhost:8080/realms/vpd";
        const string audience = "vpd-api";
        var services = new ServiceCollection();
        var configuration = new ConfigurationManager();
        configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Authentication:Provider"] = "Keycloak",
            ["Authentication:Keycloak:Authority"] = authority,
            ["Authentication:Keycloak:Audience"] = audience,
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
        schemes.Select(scheme => scheme.Name)
            .Should()
            .Contain(["Bearer", "Entra", "LegacyJwt", "Keycloak"]);

        var keycloakOptions = provider
            .GetRequiredService<IOptionsMonitor<JwtBearerOptions>>()
            .Get("Keycloak");
        keycloakOptions.Authority.Should().Be(authority);
        keycloakOptions.Audience.Should().Be(audience);
        keycloakOptions.RequireHttpsMetadata.Should().BeFalse();
        keycloakOptions.MapInboundClaims.Should().BeFalse();
        keycloakOptions.TokenValidationParameters.NameClaimType.Should().Be("name");
        keycloakOptions.TokenValidationParameters.RoleClaimType.Should().Be("roles");

        var selector = provider
            .GetRequiredService<IOptionsMonitor<PolicySchemeOptions>>()
            .Get("Bearer");
        var keycloakToken = new JwtSecurityToken(issuer: authority, audience: audience);
        var request = new DefaultHttpContext();
        request.Request.Headers.Authorization = $"Bearer {new JwtSecurityTokenHandler().WriteToken(keycloakToken)}";
        selector.ForwardDefaultSelector!(request).Should().Be("Keycloak");

        selector.ForwardDefaultSelector!(new DefaultHttpContext()).Should().Be("Keycloak");

        var entraToken = new JwtSecurityToken(
            issuer: "https://example.ciamlogin.com/tenant-id/v2.0",
            audience: "api-client-id");
        var entraRequest = new DefaultHttpContext();
        entraRequest.Request.Headers.Authorization = $"Bearer {new JwtSecurityTokenHandler().WriteToken(entraToken)}";
        selector.ForwardDefaultSelector!(entraRequest).Should().Be("Entra");

        var legacyToken = new JwtSecurityToken(issuer: "legacy-issuer", audience: "legacy-audience");
        var legacyRequest = new DefaultHttpContext();
        legacyRequest.Request.Headers.Authorization = $"Bearer {new JwtSecurityTokenHandler().WriteToken(legacyToken)}";
        selector.ForwardDefaultSelector!(legacyRequest).Should().Be("LegacyJwt");
    }

    [Fact]
    public void AddInfrastructure_RejectsKeycloakProviderWithoutLocalAuthorityAndAudience()
    {
        var services = new ServiceCollection();
        var configuration = new ConfigurationManager();
        configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Authentication:Provider"] = "Keycloak",
            ["AzureAd:Instance"] = "https://example.ciamlogin.com/",
            ["AzureAd:TenantId"] = "tenant-id",
            ["AzureAd:ClientId"] = "api-client-id",
            ["AzureAd:Audience"] = "api-client-id",
            ["JwtSettings:Audience"] = "legacy-audience",
            ["JwtSettings:Issuer"] = "legacy-issuer",
            ["JwtSettings:Secret"] = "a-secret-long-enough-for-tests",
            ["JwtSettings:ExpiryMinutes"] = "60"
        });

        var act = () => services.AddInfrastructure(configuration);

        act.Should()
            .Throw<InvalidOperationException>()
            .WithMessage("Authentication:Keycloak:Authority and Authentication:Keycloak:Audience are required when Authentication:Provider is Keycloak.");
    }

    [Fact]
    public void AddInfrastructure_RejectsRemoteHttpKeycloakAuthority()
    {
        var services = new ServiceCollection();
        var configuration = new ConfigurationManager();
        configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Authentication:Provider"] = "Keycloak",
            ["Authentication:Keycloak:Authority"] = "http://keycloak.example.test/realms/vpd",
            ["Authentication:Keycloak:Audience"] = "vpd-api",
            ["AzureAd:Instance"] = "https://example.ciamlogin.com/",
            ["AzureAd:TenantId"] = "tenant-id",
            ["AzureAd:ClientId"] = "api-client-id",
            ["AzureAd:Audience"] = "api-client-id",
            ["JwtSettings:Audience"] = "legacy-audience",
            ["JwtSettings:Issuer"] = "legacy-issuer",
            ["JwtSettings:Secret"] = "a-secret-long-enough-for-tests",
            ["JwtSettings:ExpiryMinutes"] = "60"
        });

        var act = () => services.AddInfrastructure(configuration);

        act.Should()
            .Throw<InvalidOperationException>()
            .WithMessage("Authentication:Keycloak:Authority must use HTTPS, or HTTP on a loopback address for local development.");
    }

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
        jwtOptions.Get("Entra").MapInboundClaims.Should().BeFalse();
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

    [Fact]
    public void AddInfrastructure_MapsEntraRolesForRoleBasedAuthorization()
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
        var options = provider
            .GetRequiredService<IOptionsMonitor<JwtBearerOptions>>()
            .Get("Entra");
        var token = new JwtSecurityToken(
            issuer: "https://example.ciamlogin.com/tenant-id/v2.0",
            audience: "api-client-id",
            claims: [new Claim("roles", "Administration")]);
        var tokenHandler = new JwtSecurityTokenHandler
        {
            MapInboundClaims = options.MapInboundClaims
        };
        var principal = tokenHandler.ValidateToken(
            tokenHandler.WriteToken(token),
            new Microsoft.IdentityModel.Tokens.TokenValidationParameters
            {
                ValidateIssuer = false,
                ValidateAudience = false,
                ValidateLifetime = false,
                ValidateIssuerSigningKey = false,
                RequireSignedTokens = false,
                RoleClaimType = options.TokenValidationParameters.RoleClaimType,
                SignatureValidator = (rawToken, _) => tokenHandler.ReadJwtToken(rawToken)
            },
            out _);

        principal.IsInRole("Administration").Should().BeTrue();
    }
}
