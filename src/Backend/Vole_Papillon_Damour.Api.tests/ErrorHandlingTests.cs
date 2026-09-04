using System.Diagnostics;
using System.Net;
using FluentAssertions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Vole_Papillon_Damour.Api.Errors;

namespace Vole_Papillon_Damour.Api.tests;

public sealed class ErrorHandlingTests
{
    [Fact]
    public async Task UseErrorHandling_WhenBibliographicProvidersAreUnavailable_ReturnsServiceUnavailable()
    {
        var services = new ServiceCollection()
            .AddLogging()
            .AddMetrics()
            .AddSingleton<DiagnosticListener>(_ => new DiagnosticListener("Vpd.Api.Tests"))
            .BuildServiceProvider();
        var application = new ApplicationBuilder(services);
        application.UseErrorHandling();
        application.Run(_ => throw new HttpRequestException("bibliographic providers unavailable"));
        var pipeline = application.Build();
        var context = new DefaultHttpContext
        {
            RequestServices = services
        };
        await using var responseBody = new MemoryStream();
        context.Response.Body = responseBody;

        await pipeline(context);

        context.Response.StatusCode.Should().Be((int)HttpStatusCode.ServiceUnavailable);
    }
}
