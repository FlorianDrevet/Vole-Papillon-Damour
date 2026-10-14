using System.Net;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using Vole_Papillon_Damour.Api.Controllers;
using Vole_Papillon_Damour.Application.Common.Interfaces.Persistence;
using Vole_Papillon_Damour.Application.Common.Interfaces.Services;
using Vole_Papillon_Damour.Application.Common.Services;
using Vole_Papillon_Damour.Application.ManualRequests.Commands.CreateRequester;
using Vole_Papillon_Damour.Application.ManualRequests.Commands.Refuse;
using Vole_Papillon_Damour.Application.ManualRequests.Common;
using Vole_Papillon_Damour.Domain.Common.Errors;
using Vole_Papillon_Damour.Domain.UserAggregate.ValueObjects;

namespace Vole_Papillon_Damour.Api.tests.ManualRequests;

public sealed class ManualRequestAdministrationEndpointsTests
{
    private static readonly Guid AdministratorId = Guid.Parse("6a3c9831-9999-4b4e-a4dd-6fdd2c159a0a");
    private static readonly Guid RequestId = Guid.Parse("bc3c9831-9999-4b4e-a4dd-6fdd2c159a0a");

    [Fact]
    public async Task Every_manual_request_route_returns_forbidden_without_administration_role()
    {
        await using var application = CreateApplication();
        var routes = new (string Method, string Route)[]
        {
            ("GET", "/books/admin/manual-requests"),
            ("GET", "/books/admin/manual-requests/summary"),
            ("GET", "/books/admin/manual-requests/stock"),
            ("POST", "/books/admin/manual-requests/{requestId:guid}/pick"),
            ("POST", "/books/admin/manual-requests/{requestId:guid}/given"),
            ("POST", "/books/admin/manual-requests/{requestId:guid}/refuse"),
            ("POST", "/books/admin/manual-requests/{requestId:guid}/cancel"),
            ("GET", "/books/admin/manual-requesters/{requesterId:guid}"),
            ("POST", "/books/admin/manual-requesters"),
            ("PUT", "/books/admin/manual-requesters/{requesterId:guid}"),
            ("DELETE", "/books/admin/manual-requesters/{requesterId:guid}"),
            ("POST", "/books/admin/manual-requesters/{requesterId:guid}/requests")
        };

        var statusCodes = routes.Select(route => AuthorizeEndpointAsync(
                application,
                FindEndpoint(application, route.Method, route.Route),
                Principal("Tri")))
            .ToArray();

        (await Task.WhenAll(statusCodes)).Should().OnlyContain(status =>
            status == (int)HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task CreateRequester_WithoutConsent_Returns400WithValidationCode()
    {
        var mediator = Substitute.For<IMediator>();
        mediator.Send(Arg.Any<CreateManualRequesterCommand>(), Arg.Any<CancellationToken>())
            .Returns(Vole_Papillon_Damour.Domain.Common.Errors.Errors.ManualRequest.ConsentRequired());
        await using var application = CreateApplication(mediator);
        var endpoint = FindEndpoint(application, "POST", "/books/admin/manual-requesters");

        var response = await InvokeAsync(
            application,
            endpoint,
            "POST",
            "{\"lastName\":\"Martin\",\"firstName\":\"Jeanne\",\"phone\":\"0612345678\",\"consentGiven\":false}",
            Principal("Administration"));

        response.Response.StatusCode.Should().Be((int)HttpStatusCode.BadRequest);
        var json = await ReadJsonAsync(response.Response);
        json.GetProperty("errors").TryGetProperty("ManualRequest.ConsentRequired", out _).Should().BeTrue();
        await mediator.Received(1).Send(
            Arg.Is<CreateManualRequesterCommand>(command =>
                !command.ConsentGiven && command.By == UserId.Create(AdministratorId)),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Refuse_ReturnsNotifiedCountWhenImmediateDeliveryFails()
    {
        var mediator = Substitute.For<IMediator>();
        mediator.Send(Arg.Any<RefuseManualRequestCommand>(), Arg.Any<CancellationToken>())
            .Returns(new ManualRequestReleaseResult(NotifiedMemberCount: 3));
        var alertDelivery = CreateFailingAlertDeliveryService();
        await using var application = CreateApplication(mediator, alertDelivery);
        var endpoint = FindEndpoint(
            application,
            "POST",
            "/books/admin/manual-requests/{requestId:guid}/refuse");

        var response = await InvokeAsync(
            application,
            endpoint,
            "POST",
            "{\"keepSearching\":false}",
            Principal("Administration"),
            new Dictionary<string, object?> { ["requestId"] = RequestId.ToString() });

        response.Response.StatusCode.Should().Be((int)HttpStatusCode.OK);
        var json = await ReadJsonAsync(response.Response);
        json.GetProperty("notifiedMemberCount").GetInt32().Should().Be(3);
        await mediator.Received(1).Send(
            Arg.Is<RefuseManualRequestCommand>(command =>
                command.RequestId == RequestId &&
                !command.KeepSearching &&
                command.By == UserId.Create(AdministratorId)),
            Arg.Any<CancellationToken>());
    }

    private static BookAlertDeliveryService CreateFailingAlertDeliveryService()
    {
        var outbox = Substitute.For<IBookAlertOutbox>();
        outbox.GetOldestDueAtAsync(Arg.Any<DateTime>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromException<DateTime?>(new InvalidOperationException("Outbox unavailable")));
        var emailSender = Substitute.For<IBookAlertEmailSender>();
        emailSender.IsEnabled.Returns(true);
        var clock = Substitute.For<IDateTimeProvider>();
        clock.UtcNow.Returns(DateTime.UtcNow);
        return new BookAlertDeliveryService(outbox, emailSender, clock);
    }

    private static WebApplication CreateApplication(
        IMediator? mediator = null,
        BookAlertDeliveryService? alertDeliveryService = null)
    {
        var builder = WebApplication.CreateBuilder();
        builder.Services.AddAuthorization(options =>
            options.AddPolicy("Administration", policy => policy.RequireRole("Administration", "Admin")));
        builder.Services.AddSingleton(mediator ?? Substitute.For<IMediator>());
        if (alertDeliveryService is not null)
        {
            builder.Services.AddSingleton(alertDeliveryService);
        }

        var application = builder.Build();
        application.UseRouting();
        application.UseManualRequestAdministrationController();
        return application;
    }

    private static RouteEndpoint? FindEndpoint(
        WebApplication application,
        string method,
        string route) =>
        application.Services.GetRequiredService<EndpointDataSource>()
            .Endpoints.OfType<RouteEndpoint>()
            .SingleOrDefault(endpoint =>
                endpoint.RoutePattern.RawText == route &&
                endpoint.Metadata.GetMetadata<HttpMethodMetadata>()?.HttpMethods.Contains(method) == true);

    private static async Task<DefaultHttpContext> InvokeAsync(
        WebApplication application,
        RouteEndpoint? endpoint,
        string method,
        string? body = null,
        ClaimsPrincipal? principal = null,
        IDictionary<string, object?>? routeValues = null)
    {
        var context = new DefaultHttpContext
        {
            RequestServices = application.Services,
            User = principal ?? Principal("Administration")
        };
        context.Request.Method = method;
        context.Request.ContentType = "application/json";
        var requestBody = Encoding.UTF8.GetBytes(body ?? string.Empty);
        context.Request.Body = new MemoryStream(requestBody);
        context.Request.ContentLength = requestBody.Length;
        context.Features.Set<IHttpRequestBodyDetectionFeature>(new TestRequestBodyDetectionFeature());
        context.Response.Body = new MemoryStream();
        if (endpoint is null)
        {
            context.Response.StatusCode = (int)HttpStatusCode.NotFound;
            return context;
        }

        context.SetEndpoint(endpoint);
        if (routeValues is not null)
        {
            foreach (var (key, value) in routeValues)
            {
                context.Request.RouteValues[key] = value;
            }
        }

        await endpoint.RequestDelegate!(context);
        return context;
    }

    private static async Task<int> AuthorizeEndpointAsync(
        WebApplication application,
        RouteEndpoint? endpoint,
        ClaimsPrincipal principal)
    {
        var policyName = endpoint?.Metadata.GetOrderedMetadata<IAuthorizeData>()
            .SingleOrDefault()?.Policy;
        policyName.Should().Be("Administration");
        var authorization = application.Services.GetRequiredService<IAuthorizationService>();
        var result = await authorization.AuthorizeAsync(principal, resource: null, policyName!);
        return result.Succeeded
            ? (int)HttpStatusCode.OK
            : principal.Identity?.IsAuthenticated == true
                ? (int)HttpStatusCode.Forbidden
                : (int)HttpStatusCode.Unauthorized;
    }

    private static async Task<JsonElement> ReadJsonAsync(HttpResponse response)
    {
        response.Body.Position = 0;
        using var document = await JsonDocument.ParseAsync(response.Body);
        return document.RootElement.Clone();
    }

    private sealed class TestRequestBodyDetectionFeature : IHttpRequestBodyDetectionFeature
    {
        public bool CanHaveBody => true;
    }

    private static ClaimsPrincipal Principal(string role) => new(
        new ClaimsIdentity(
        [
            new Claim("oid", AdministratorId.ToString()),
            new Claim(ClaimTypes.Role, role)
        ],
        authenticationType: "Test"));
}
