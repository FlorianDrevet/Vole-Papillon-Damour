using System.Text;
using Azure;
using Azure.AI.Vision.ImageAnalysis;
using Azure.Communication.Email;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Azure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Vole_Papillon_Damour.Application.Common.Interfaces.Authentication;
using Vole_Papillon_Damour.Application.Common.Interfaces.Persistence;
using Vole_Papillon_Damour.Application.Common.Interfaces.Services;
using Vole_Papillon_Damour.Infrastructure.Authentication;
using Vole_Papillon_Damour.Infrastructure.Persistence;
using Vole_Papillon_Damour.Infrastructure.Persistence.Repositories;
using Vole_Papillon_Damour.Infrastructure.Services;
using Vole_Papillon_Damour.Infrastructure.Services.BlobService;
using Vole_Papillon_Damour.Infrastructure.Services.EmailService;
using Vole_Papillon_Damour.Infrastructure.Services.ExtractNumbersOcrService;
using Vole_Papillon_Damour.Infrastructure.Services.OcrService;
using Vole_Papillon_Damour.Infrastructure.Services.TableStorageService;

namespace Vole_Papillon_Damour.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        ConfigurationManager builderConfiguration)
    {
        var connectionString = builderConfiguration.GetConnectionString("ProjectDatabase");
            
        services
            .AddAuth(builderConfiguration)
            .AddDbContext<ProjectDbContext>(options =>
                options.UseSqlServer(connectionString)
                )
            .AddAzureServices(builderConfiguration)
            .AddStorageAccounts(builderConfiguration)
            .AddRepositories()
            .AddOcr(builderConfiguration)
            .AddEmail(builderConfiguration)
            .AddScoped<IProjectDbContext>(provider => provider.GetRequiredService<ProjectDbContext>());
        
        services.AddSingleton<IDateTimeProvider, DateTimeProvider>();
        services.AddSingleton<ISSEClientManager, SSEClientManager>();
        services.AddScoped<IExtractNumbersOcrService, ExtractNumbersOcrService>();
        
        return services;
    }

    private static IServiceCollection AddOcr(
        this IServiceCollection services,
        ConfigurationManager builderConfiguration)
    {
        var ocrSettings = new OcrSettings();
        builderConfiguration.Bind(OcrSettings.SectionName, ocrSettings);

        services.AddSingleton(new ImageAnalysisClient(new Uri(ocrSettings.VisionEndpoint),
            new AzureKeyCredential(ocrSettings.VisionKey)));
        services.AddScoped<IOcrService, OcrService>();
        
        return services;
    }
    
    private static IServiceCollection AddEmail(
        this IServiceCollection services,
        ConfigurationManager builderConfiguration)
    {
        services.AddSingleton(new EmailClient(builderConfiguration.GetConnectionString("EmailService")));
        services.AddScoped<IEmailService, EmailService>();
        
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
        ConfigurationManager builderConfiguration)
    {
        services.AddAzureClients(clientBuilder =>
        {
            // Blob Service
            string connectionString = builderConfiguration.GetConnectionString("AzureBlobStorageConnectionString") ?? string.Empty;
            clientBuilder.AddBlobServiceClient(connectionString);
            clientBuilder.AddTableServiceClient(connectionString).WithName("MailingListStorage");
        });
        return services;
    }
    
    private static IServiceCollection AddStorageAccounts(
        this IServiceCollection services,
        ConfigurationManager builderConfiguration)
    {
        var blobSettings = new BlobSettings();
        builderConfiguration.Bind(BlobSettings.SectionName, blobSettings);

        services.AddSingleton(Options.Create(blobSettings));
        services.AddSingleton<IBlobService, BlobService>();
        services.AddSingleton<ITableStorageService, TableStorageService>();
        return services;
    }


    
    private static IServiceCollection AddAuth(
        this IServiceCollection services,
        ConfigurationManager builderConfiguration)
    {
        var jwtSettings = new JwtSettings();
        builderConfiguration.Bind(JwtSettings.SectionName, jwtSettings);

        services.AddSingleton(Options.Create(jwtSettings));
        services.AddSingleton<IJwtGenerator, JwtGenerator>();
        services.AddSingleton<IHashPassword, HashPassword>();
        services.AddAuthentication(defaultScheme: JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options => options.TokenValidationParameters = new TokenValidationParameters
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
            });
        return services;
    }
}