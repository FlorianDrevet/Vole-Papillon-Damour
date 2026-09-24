using System.Net;
using System.Security.Claims;
using System.Text;
using FluentAssertions;
using MapsterMapper;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using Vole_Papillon_Damour.Api.Controllers;
using Vole_Papillon_Damour.Application.Common.Interfaces.Services;
using Vole_Papillon_Damour.Application.MemberSelection.Commands.AddSelectionItem;
using Vole_Papillon_Damour.Application.MemberSelection.Common;

namespace Vole_Papillon_Damour.Api.tests.MemberAccount;

public sealed class MemberSelectionEndpointsTests
{
    private static readonly Guid ExternalId = Guid.Parse("7f0b0f0e-0000-0000-0000-00000000c0de");

    [Fact]
    public async Task GetSelection_WithoutAuthentication_Returns401()
    {
        await using var application = CreateApplication();
        var endpoint = FindEndpoint(application, "GET", "/catalog/me/selection");

        var response = await InvokeAsync(application, endpoint, "GET", principal: new ClaimsPrincipal());

        response.Response.StatusCode.Should().Be((int)HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task PatchSelection_WithUnknownStatus_Returns400()
    {
        await using var application = CreateApplication();
        var endpoint = FindEndpoint(application, "PATCH", "/catalog/me/selection/{id:guid}");

        var response = await InvokeAsync(
            application,
            endpoint,
            "PATCH",
            "{\"status\":\"Reserved\"}",
            AuthenticatedPrincipal(),
            new Dictionary<string, object?> { ["id"] = Guid.NewGuid().ToString() });

        response.Response.StatusCode.Should().Be((int)HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task PostSelection_ForwardsIdentityAndTargetToMediator()
    {
        var mediator = Substitute.For<IMediator>();
        mediator.Send(Arg.Any<AddSelectionItemCommand>(), Arg.Any<CancellationToken>())
            .Returns(new SelectionItemAddedResult(
                Guid.Parse("11111111-1111-1111-1111-111111111111"),
                AlreadyPresent: false));
        await using var application = CreateApplication(mediator);
        var endpoint = FindEndpoint(application, "POST", "/catalog/me/selection");

        var response = await InvokeAsync(
            application,
            endpoint,
            "POST",
            "{\"isbn13\":\"9782070612758\"}",
            AuthenticatedPrincipal());

        response.Response.StatusCode.Should().Be((int)HttpStatusCode.OK);

        await mediator.Received(1).Send(
            Arg.Is<AddSelectionItemCommand>(command =>
                command.ExternalId == ExternalId &&
                command.Email == "camille@example.test" &&
                command.FirstName == "Camille" &&
                command.LastName == "Durand" &&
                command.Isbn13 == "9782070612758" &&
                command.RareBookId == null),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task EveryMemberSelectionEndpointRequiresAuthorization()
    {
        await using var application = CreateApplication();
        var endpoints = application.Services.GetRequiredService<EndpointDataSource>()
            .Endpoints.OfType<RouteEndpoint>()
            .Where(endpoint => endpoint.RoutePattern.RawText?.StartsWith(
                "/catalog/me/selection", StringComparison.Ordinal) == true)
            .ToArray();

        endpoints.Should().HaveCount(5);
        endpoints.Should().OnlyContain(endpoint =>
            endpoint.Metadata.GetOrderedMetadata<Microsoft.AspNetCore.Authorization.IAuthorizeData>().Count > 0);
    }

    private static WebApplication CreateApplication(IMediator? mediator = null)
    {
        var builder = WebApplication.CreateBuilder();
        builder.Services.AddAuthorization();
        builder.Services.AddSingleton(mediator ?? Substitute.For<IMediator>());
        builder.Services.AddSingleton(Substitute.For<IMapper>());
        builder.Services.AddSingleton(Substitute.For<ISSEClientManager>());
        builder.Services.AddSingleton(Substitute.For<IAccountDeletionService>());

        var application = builder.Build();
        application.UseRouting();
        application.UseBookController();
        application.UseMemberAccountController();
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
            User = principal ?? AuthenticatedPrincipal()
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

    private sealed class TestRequestBodyDetectionFeature : IHttpRequestBodyDetectionFeature
    {
        public bool CanHaveBody => true;
    }

    private static ClaimsPrincipal AuthenticatedPrincipal() => new(
        new ClaimsIdentity(
        [
            new Claim("oid", ExternalId.ToString()),
            new Claim("email", "camille@example.test"),
            new Claim("given_name", "Camille"),
            new Claim("family_name", "Durand")
        ],
        authenticationType: "Test"));
}
