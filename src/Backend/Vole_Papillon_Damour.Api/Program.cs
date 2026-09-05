using System.Text.Json;
using Azure.Monitor.OpenTelemetry.AspNetCore;
using Vole_Papillon_Damour.Api;
using Vole_Papillon_Damour.Api.Common;
using Vole_Papillon_Damour.Api.Common.Mapping;
using Vole_Papillon_Damour.Api.Common.RateLimiting;
using Vole_Papillon_Damour.Api.Controllers;
using Vole_Papillon_Damour.Api.Controllers.AssoEventsController;
using Vole_Papillon_Damour.Api.Errors;
using Vole_Papillon_Damour.Api.Integrations.AcsEmail;
using Vole_Papillon_Damour.Application;
using Vole_Papillon_Damour.Infrastructure;
using Vole_Papillon_Damour.Infrastructure.Health;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddCors(options =>
{
    options.AddPolicy("CorsPolicy", policy =>
    {
        var allowedOrigins = builder.Configuration
            .GetSection("Cors:AllowedOrigins")
            .Get<string[]>() ?? [];
        policy.AllowAnyHeader().AllowAnyMethod();
        if (allowedOrigins.Length == 0)
        {
            policy.AllowAnyOrigin();
        }
        else
        {
            policy.WithOrigins(allowedOrigins);
        }
    });
});

builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
        options.JsonSerializerOptions.WriteIndented = true;
    });

builder.Services.AddAuthorizationBuilder()
    // `IsAdmin` remains a compatibility alias during the staged migration. The
    // Entra role is authoritative for new tokens; `Admin` keeps existing JWT
    // sessions usable until the final deployment removes the legacy scheme.
    .AddPolicy("Administration", policy => policy.RequireRole("Administration", "Admin"))
    .AddPolicy("Tri", policy => policy.RequireRole("Tri"))
    .AddPolicy("Caisse", policy => policy.RequireRole("Caisse"))
    .AddPolicy("IsAdmin", policy => policy.RequireRole("Administration", "Admin"));

var applicationInsightsConnectionString = builder.Configuration["APPLICATIONINSIGHTS_CONNECTION_STRING"];
if (!string.IsNullOrWhiteSpace(applicationInsightsConnectionString))
{
    builder.Services.AddOpenTelemetry().UseAzureMonitor();
}

builder.Services
    .AddPresentation()
    .AddApplication()
    .AddInfrastructure(
        builder.Configuration,
        runMigrations: DatabaseMigrationPolicy.ShouldRunOnStartup(
            builder.Environment.EnvironmentName))
    .AddBookMetadataEnrichmentProcessing()
    .AddRateLimiting();

builder.Services.Configure<EmailBounceWebhookOptions>(
    builder.Configuration.GetSection(EmailBounceWebhookOptions.SectionName));

builder.Services
    .AddHealthChecks()
    .AddInfrastructureHealthChecks();


var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseWebSockets();

//Middleware
app.UseCors("CorsPolicy");

app.UseErrorHandling();
app.UseHttpsRedirection();
app.UseRouting();
app.UseRateLimiter(); //After UseRouting
app.UseStatusCodePages();
app.UseAuthentication();
app.UseAuthorization();

app.MapHealthChecks("/health");

//Controllers
app.UseAcsEmailEventGridController();
app.UseAuthenticationController();
app.UseAccountController();
app.UseBookController();
app.UseActualityController();
app.UseProductController();
app.UseOrdersController();
app.UseEventsController();

app.Run();
