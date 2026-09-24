using System.Net;
using System.Security.Claims;
using System.Text;
using ErrorOr;
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
using Vole_Papillon_Damour.Application.NotFoundReports.Commands.CancelNotFoundReport;
using Vole_Papillon_Damour.Application.NotFoundReports.Commands.ReportNotFound;
using Vole_Papillon_Damour.Application.NotFoundReports.Common;
using Vole_Papillon_Damour.Domain.Common.Errors;

namespace Vole_Papillon_Damour.Api.tests.MemberAccount;

public sealed class NotFoundReportEndpointsTests
{
    private static readonly Guid ExternalId = Guid.Parse("7f0b0f0e-0000-0000-0000-00000000c0de");
    private static readonly Guid SelectionItemId = Guid.Parse("11111111-2222-3333-4444-555555555555");
    private static readonly Guid ReportId = Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee");
    private static readonly DateTimeOffset ReportedAt = new(2026, 9, 24, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task PostReport_WithoutAuthentication_Returns401()
    {
        await using var application = CreateApplication();
        var endpoint = FindEndpoint(
            application,
            "POST",
            "/catalog/me/selection/{id:guid}/not-found-report");

        var response = await InvokeAsync(
            application,
            endpoint,
            "POST",
            "{}",
            new ClaimsPrincipal(),
            new Dictionary<string, object?> { ["id"] = SelectionItemId.ToString() });

        response.Response.StatusCode.Should().Be((int)HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task PostReport_ForwardsIdentityItemAndPayload()
    {
        var mediator = Substitute.For<IMediator>();
        mediator.Send(Arg.Any<ReportNotFoundCommand>(), Arg.Any<CancellationToken>())
            .Returns(new NotFoundReportCreatedResult(ReportId, ReportedAt, AlreadyOpen: false));
        await using var application = CreateApplication(mediator);
        var endpoint = FindEndpoint(
            application,
            "POST",
            "/catalog/me/selection/{id:guid}/not-found-report");

        var response = await InvokeAsync(
            application,
            endpoint,
            "POST",
            "{\"location\":\"Premises\",\"comment\":\"not on shelf\"}",
            AuthenticatedPrincipal(),
            new Dictionary<string, object?> { ["id"] = SelectionItemId.ToString() });

        response.Response.StatusCode.Should().Be((int)HttpStatusCode.Created);
        await mediator.Received(1).Send(
            Arg.Is<ReportNotFoundCommand>(command =>
                command.ExternalId == ExternalId &&
                command.Email == "camille@example.test" &&
                command.FirstName == "Camille" &&
                command.LastName == "Durand" &&
                command.SelectionItemId == SelectionItemId &&
                command.Location == Domain.NotFoundReportAggregate.ValueObjects.NotFoundReportLocation.Premises &&
                command.Comment == "not on shelf"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task PostReport_WhenAlreadyOpen_Returns200()
    {
        var mediator = Substitute.For<IMediator>();
        mediator.Send(Arg.Any<ReportNotFoundCommand>(), Arg.Any<CancellationToken>())
            .Returns(new NotFoundReportCreatedResult(ReportId, ReportedAt, AlreadyOpen: true));
        await using var application = CreateApplication(mediator);
        var endpoint = FindEndpoint(
            application,
            "POST",
            "/catalog/me/selection/{id:guid}/not-found-report");

        var response = await InvokeAsync(
            application,
            endpoint,
            "POST",
            "{}",
            AuthenticatedPrincipal(),
            new Dictionary<string, object?> { ["id"] = SelectionItemId.ToString() });

        response.Response.StatusCode.Should().Be((int)HttpStatusCode.OK);
    }

    [Fact]
    public async Task PostReport_WhenLimitReached_Returns429()
    {
        var mediator = Substitute.For<IMediator>();
        mediator.Send(Arg.Any<ReportNotFoundCommand>(), Arg.Any<CancellationToken>())
            .Returns(Vole_Papillon_Damour.Domain.Common.Errors.Errors.NotFoundReport.DailyLimitReached(10));
        await using var application = CreateApplication(mediator);
        var endpoint = FindEndpoint(
            application,
            "POST",
            "/catalog/me/selection/{id:guid}/not-found-report");

        var response = await InvokeAsync(
            application,
            endpoint,
            "POST",
            "{}",
            AuthenticatedPrincipal(),
            new Dictionary<string, object?> { ["id"] = SelectionItemId.ToString() });

        response.Response.StatusCode.Should().Be((int)HttpStatusCode.TooManyRequests);
    }

    [Fact]
    public async Task DeleteReport_Returns204()
    {
        var mediator = Substitute.For<IMediator>();
        mediator.Send(Arg.Any<CancelNotFoundReportCommand>(), Arg.Any<CancellationToken>())
            .Returns(Result.Deleted);
        await using var application = CreateApplication(mediator);
        var endpoint = FindEndpoint(
            application,
            "DELETE",
            "/catalog/me/not-found-reports/{reportId:guid}");

        var response = await InvokeAsync(
            application,
            endpoint,
            "DELETE",
            principal: AuthenticatedPrincipal(),
            routeValues: new Dictionary<string, object?> { ["reportId"] = ReportId.ToString() });

        response.Response.StatusCode.Should().Be((int)HttpStatusCode.NoContent);
        await mediator.Received(1).Send(
            Arg.Is<CancelNotFoundReportCommand>(command =>
                command.ExternalId == ExternalId && command.ReportId == ReportId),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task NotFoundReportRoutes_RequireAuthorization()
    {
        await using var application = CreateApplication();
        var endpoints = application.Services.GetRequiredService<EndpointDataSource>()
            .Endpoints.OfType<RouteEndpoint>()
            .Where(endpoint => endpoint.RoutePattern.RawText is
                "/catalog/me/selection/{id:guid}/not-found-report" or
                "/catalog/me/not-found-reports/{reportId:guid}")
            .ToArray();

        endpoints.Should().HaveCount(2);
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
