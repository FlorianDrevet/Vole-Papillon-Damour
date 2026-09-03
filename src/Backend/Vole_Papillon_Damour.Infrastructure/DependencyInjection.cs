using System.Text;
using System.IdentityModel.Tokens.Jwt;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Azure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Microsoft.Identity.Web;
using Vole_Papillon_Damour.Application.Common.Interfaces.Authentication;
using Vole_Papillon_Damour.Application.Common.Interfaces.Persistence;
using Vole_Papillon_Damour.Application.Common.Interfaces.Services;
using Vole_Papillon_Damour.Infrastructure.AccountDeletion;
using Vole_Papillon_Damour.Infrastructure.Authentication;
using Vole_Papillon_Damour.Infrastructure.Extensions;
using Vole_Papillon_Damour.Infrastructure.Persistence;
using Vole_Papillon_Damour.Infrastructure.Persistence.Repositories;
using Vole_Papillon_Damour.Infrastructure.Services;
using Vole_Papillon_Damour.Infrastructure.Services.Bibliographic;
using Vole_Papillon_Damour.Infrastructure.Services.BookAlerts;
using Vole_Papillon_Damour.Infrastructure.Services.BlobService;

namespace Vole_Papillon_Damour.Infrastructure;

public static class DependencyInjection
{
    private const string CompositeAuthenticationScheme = "Bearer";
    private const string EntraAuthenticationScheme = "Entra";
    private const string LegacyAuthenticationScheme = "LegacyJwt";
    private const string AzureBlobStorageConnectionStringName = "AzureBlobStorageConnectionString";
    private const string ProjectDatabaseConnectionStringName = "ProjectDatabase";

    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration builderConfiguration,
        bool runMigrations = true,
        bool registerAuthentication = true)
    {
        var connectionString = builderConfiguration.GetConnectionString(ProjectDatabaseConnectionStringName);

        if (registerAuthentication)
        {
            services.AddAuth(builderConfiguration);
        }

        services
            .AddDbContext<ProjectDbContext>(options =>
                options.UseSqlServer(connectionString)
                )
            .AddAzureServices(builderConfiguration)
            .AddStorageAccounts(builderConfiguration)
            .AddRepositories()
            .AddScoped<IProjectDbContext>(provider => provider.GetRequiredService<ProjectDbContext>());

        if (runMigrations)
        {
            services.AddMigration<ProjectDbContext>();
        }
        
        services.AddSingleton<IDateTimeProvider, DateTimeProvider>();
        services.AddSingleton<ISSEClientManager, SSEClientManager>();
        services.AddSingleton<IUserDeletionRetentionPolicy, NoRetainedSalesMovementsPolicy>();
        services.AddScoped<IAccountDeletionStore, AccountDeletionStore>();
        services.AddScoped<IBookAlertOutbox, BookAlertOutbox>();
        services.Configure<EntraGraphOptions>(builderConfiguration.GetSection(EntraGraphOptions.SectionName));
        services.AddHttpClient<IEntraUserDirectory, EntraGraphUserDirectory>();
        services.Configure<BibliographicOptions>(
            builderConfiguration.GetSection(BibliographicOptions.SectionName));
        services.AddHttpClient<IBnfSruClient, BnfSruClient>((serviceProvider, client) =>
        {
            var options = serviceProvider.GetRequiredService<IOptions<BibliographicOptions>>().Value;
            client.Timeout = TimeSpan.FromMilliseconds(options.BnfTimeoutMilliseconds);
            client.DefaultRequestHeaders.UserAgent.ParseAdd(options.UserAgent);
        });
        services.AddHttpClient<IOpenLibraryClient, OpenLibraryClient>((serviceProvider, client) =>
        {
            var options = serviceProvider.GetRequiredService<IOptions<BibliographicOptions>>().Value;
            client.Timeout = TimeSpan.FromMilliseconds(options.OpenLibraryTimeoutMilliseconds);
            client.DefaultRequestHeaders.UserAgent.ParseAdd(options.UserAgent);
        });
        services.AddScoped<IBibliographicMetadataResolver, BibliographicMetadataResolver>();
        
        return services;
    }
    
    private static IServiceCollection AddRepositories(
        this IServiceCollection services)
    {
        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<IProductRepository, ProductRepository>();
        services.AddScoped<IEventRepository, EventRepository>();
        services.AddScoped<IOrderRepository, OrderRepository>();
        services.AddScoped<IActualityRepository, ActualityRepository>();
        return services;
    }
    
    private static IServiceCollection AddAzureServices(
        this IServiceCollection services,
        IConfiguration builderConfiguration)
    {
        services.AddAzureClients(clientBuilder =>
        {
            // Blob Service
            string connectionString = builderConfiguration.GetConnectionString(AzureBlobStorageConnectionStringName) ?? string.Empty;
            clientBuilder.AddBlobServiceClient(connectionString);
        });
        return services;
    }
    
    private static IServiceCollection AddStorageAccounts(
        this IServiceCollection services,
        IConfiguration builderConfiguration)
    {
        var blobSettings = new BlobSettings();
        builderConfiguration.Bind(BlobSettings.SectionName, blobSettings);

        services.AddSingleton(Options.Create(blobSettings));
        services.AddSingleton<IBlobService, BlobService>();
        return services;
    }


    
    private static IServiceCollection AddAuth(
        this IServiceCollection services,
        IConfiguration builderConfiguration)
    {
        var jwtSettings = new JwtSettings();
        builderConfiguration.Bind(JwtSettings.SectionName, jwtSettings);

        services.AddSingleton(Options.Create(jwtSettings));
        services.AddSingleton<IJwtGenerator, JwtGenerator>();
        services.AddSingleton<IHashPassword, HashPassword>();

        services
            .AddAuthentication(options =>
            {
                options.DefaultAuthenticateScheme = CompositeAuthenticationScheme;
                options.DefaultChallengeScheme = CompositeAuthenticationScheme;
                options.DefaultScheme = CompositeAuthenticationScheme;
            })
            .AddPolicyScheme(CompositeAuthenticationScheme, null, options =>
            {
                options.ForwardDefaultSelector = SelectAuthenticationScheme;
            })
            .AddJwtBearer(LegacyAuthenticationScheme, options => options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidateAudience = true,
                ValidateLifetime = true,
                ValidateIssuerSigningKey = true,
                ValidIssuer = jwtSettings.Issuer,
                ValidAudience = jwtSettings.Audience,
                IssuerSigningKey = new SymmetricSecurityKey(
                    Encoding.UTF8.GetBytes(jwtSettings.Secret)
                    ),
                RoleClaimType = "role"
            })
            .AddMicrosoftIdentityWebApi(
                builderConfiguration.GetSection("AzureAd"),
                EntraAuthenticationScheme);

        services.PostConfigure<JwtBearerOptions>(EntraAuthenticationScheme, options =>
        {
            // Keep Entra's standard `roles` claim name so ASP.NET Core role
            // policies can evaluate app roles such as `Administration`.
            options.MapInboundClaims = false;
            options.TokenValidationParameters.RoleClaimType = "roles";
        });

        return services;
    }

    private static string SelectAuthenticationScheme(HttpContext context)
    {
        var authorization = context.Request.Headers.Authorization.ToString();
        if (!authorization.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
        {
            return EntraAuthenticationScheme;
        }

        var token = authorization["Bearer ".Length..].Trim();
        if (string.IsNullOrWhiteSpace(token))
        {
            return EntraAuthenticationScheme;
        }

        try
        {
            var issuer = new JwtSecurityTokenHandler().ReadJwtToken(token).Issuer;
            return issuer.Contains(".ciamlogin.com", StringComparison.OrdinalIgnoreCase)
                ? EntraAuthenticationScheme
                : LegacyAuthenticationScheme;
        }
        catch (ArgumentException)
        {
            return LegacyAuthenticationScheme;
        }
    }
}
