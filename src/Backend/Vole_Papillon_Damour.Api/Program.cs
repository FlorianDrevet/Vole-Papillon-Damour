using System.Text.Json;
using Azure.Monitor.OpenTelemetry.AspNetCore;
using Microsoft.AspNetCore.HttpOverrides;
using Vole_Papillon_Damour.Application.Common.Observability;
using Vole_Papillon_Damour.Api;
using Vole_Papillon_Damour.Api.Common;
using Vole_Papillon_Damour.Api.Common.Mapping;
using Vole_Papillon_Damour.Api.Common.Observability;
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

// Container Apps' ingress terminates the client connection and forwards to the
// app over its internal network, so `HttpContext.Connection.RemoteIpAddress`
// is otherwise always the ingress's own address. Trusting the forwarded
// headers unconditionally is safe here: the ingress is the only way to reach
// this container from outside it.
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    options.KnownIPNetworks.Clear();
    options.KnownProxies.Clear();
});

builder.Services.AddOutputCache();

builder.Services.AddCors(options =>
{
    options.AddPolicy("CorsPolicy", policy =>
    {
        var allowedOrigins = builder.Configuration
            .GetSection("Cors:AllowedOrigins")
            .Get<string[]>() ?? [];
        policy
            .AllowAnyHeader()
            .AllowAnyMethod()
            .WithExposedHeaders("Request-Context");
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
    .AddPolicy("RareBooks", policy => policy.RequireRole("LivresRares", "Administration", "Admin"))
    .AddPolicy("Tri", policy => policy.RequireRole("Tri"))
    .AddPolicy("Caisse", policy => policy.RequireRole("Caisse"))
    .AddPolicy("ScanVolunteer", policy => policy.RequireRole("Tri", "Caisse"))
    .AddPolicy("IsAdmin", policy => policy.RequireRole("Administration", "Admin"));

var applicationInsightsConnectionString = builder.Configuration["APPLICATIONINSIGHTS_CONNECTION_STRING"];
if (!string.IsNullOrWhiteSpace(applicationInsightsConnectionString))
{
    builder.Services
        .AddOpenTelemetry()
        .UseAzureMonitor(AzureMonitorTelemetry.ConfigureSampling)
        .WithTracing(tracing => tracing.AddSource(BookScanTelemetry.ActivitySourceName))
        .WithMetrics(metrics => metrics.AddMeter(BookScanTelemetry.MeterName));
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

app.UseForwardedHeaders();
app.UseWebSockets();

//Middleware
app.UseCors("CorsPolicy");

app.UseErrorHandling();
app.UseHttpsRedirection();
app.UseRouting();
app.UseOutputCache();
app.UseStatusCodePages();
app.UseAuthentication();
app.UseAuthorization();
app.UseRateLimiter(); //After authentication so per-member policies can partition by oid.

app.MapHealthChecks("/health");

//Controllers
app.UseAcsEmailEventGridController();
app.UseEmailUnsubscribeController();
app.UseAuthenticationController();
app.UseAccountController();
app.UseAccountAdministrationController();
app.UseBookController();
app.UseMemberAccountController();
app.UseCheckoutPassageController();
app.UseRareBookController();
app.UseBibliographicReferenceController();
app.UseBookAdministrationController();
app.UseActualityController();
app.UseProductController();
app.UseOrdersController();
app.UseEventsController();

app.Run();
