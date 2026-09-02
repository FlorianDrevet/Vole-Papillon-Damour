using System.Text.Json;
using Azure.Monitor.OpenTelemetry.AspNetCore;
using Vole_Papillon_Damour.Api;
using Vole_Papillon_Damour.Api.Common.Mapping;
using Vole_Papillon_Damour.Api.Common.RateLimiting;
using Vole_Papillon_Damour.Api.Controllers;
using Vole_Papillon_Damour.Api.Controllers.AssoEventsController;
using Vole_Papillon_Damour.Api.Errors;
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
        policy.AllowAnyHeader()
            .AllowAnyMethod()
            .AllowAnyOrigin();
    });
});

builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
        options.JsonSerializerOptions.WriteIndented = true;
    });

builder.Services.AddAuthorizationBuilder()
    .AddPolicy("IsAdmin", policy => policy.RequireRole("Admin"));

var applicationInsightsConnectionString = builder.Configuration["APPLICATIONINSIGHTS_CONNECTION_STRING"];
if (!string.IsNullOrWhiteSpace(applicationInsightsConnectionString))
{
    builder.Services.AddOpenTelemetry().UseAzureMonitor();
}

builder.Services
    .AddPresentation()
    .AddApplication()
    .AddInfrastructure(builder.Configuration)
    .AddRateLimiting();

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
app.UseAuthenticationController();
app.UseActualityController();
app.UseProductController();
app.UseOrdersController();
app.UseEventsController();

app.Run();
