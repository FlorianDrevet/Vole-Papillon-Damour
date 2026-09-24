using System.Net;
using System.Security.Claims;
using System.Text;
using FluentAssertions;
using MapsterMapper;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using Vole_Papillon_Damour.Api.Controllers;
using Vole_Papillon_Damour.Application.Common.Interfaces.Services;
using Vole_Papillon_Damour.Application.NotFoundReports.Commands.Admin.MarkFound;
using Vole_Papillon_Damour.Application.NotFoundReports.Commands.Admin.WithdrawNotFound;
using Vole_Papillon_Damour.Application.NotFoundReports.Common;
using Vole_Papillon_Damour.Application.NotFoundReports.Queries.GetNotFoundReportQueue;
using Vole_Papillon_Damour.Domain.Common.Errors;
using Vole_Papillon_Damour.Domain.NotFoundReportAggregate.ValueObjects;
using Vole_Papillon_Damour.Domain.UserAggregate.ValueObjects;

namespace Vole_Papillon_Damour.Api.tests.NotFoundReports;

public sealed class NotFoundReportAdministrationEndpointsTests
{
    private static readonly Guid VolunteerId = Guid.Parse("6a3c9831-9999-4b4e-a4dd-6fdd2c159a0a");
    private static readonly Guid RareBookId = Guid.Parse("ac3c9831-9999-4b4e-a4dd-6fdd2c159a0a");
    private const string Isbn = "9782070612758";

    [Fact]
    public async Task GetQueue_WithoutAdministrationOrRareBooks_Returns403()
    {
        await using var application = CreateApplication();
        var endpoint = FindEndpoint(application, "GET", "/books/admin/not-found-reports");

        var statusCode = await AuthorizeEndpointAsync(
            application,
            endpoint,
            new ClaimsPrincipal(new ClaimsIdentity(
                [new Claim(ClaimTypes.Role, "Caisse")],
                authenticationType: "Test")));

        statusCode.Should().Be((int)HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task GetQueue_ForRareBooksOnlyUser_SendsRareOnlyTrue()
    {
        var mediator = Substitute.For<IMediator>();
        mediator.Send(Arg.Any<GetNotFoundReportQueueQuery>(), Arg.Any<CancellationToken>())
            .Returns(new NotFoundReportQueueResult(
                DateTimeOffset.UtcNow, 0, 0, 0, 1, 20, 0, Array.Empty<NotFoundReportTargetResult>()));
        await using var application = CreateApplication(mediator);
        var endpoint = FindEndpoint(application, "GET", "/books/admin/not-found-reports");

        var response = await InvokeAsync(
            application,
            endpoint,
            "GET",
            principal: Principal("LivresRares"));

        response.Response.StatusCode.Should().Be((int)HttpStatusCode.OK);
        await mediator.Received(1).Send(
            Arg.Is<GetNotFoundReportQueueQuery>(query => query.RareOnly),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task PostEditionWithdrawal_ForwardsQuantityReasonNoteAndAuthor()
    {
        var mediator = Substitute.For<IMediator>();
        mediator.Send(Arg.Any<WithdrawNotFoundTargetCommand>(), Arg.Any<CancellationToken>())
            .Returns(new NotFoundClosureResult(2, 1, 3, Guid.NewGuid()));
        await using var application = CreateApplication(mediator);
        var endpoint = FindEndpoint(
            application,
            "POST",
            "/books/admin/not-found-reports/editions/{isbn13}/withdrawal");

        var response = await InvokeAsync(
            application,
            endpoint,
            "POST",
            "{\"quantityFound\":1,\"reason\":\"Damaged\",\"note\":\"Retiré après vérification\"}",
            Principal("Administration"),
            new Dictionary<string, object?> { ["isbn13"] = Isbn });

        response.Response.StatusCode.Should().Be((int)HttpStatusCode.OK);
        await mediator.Received(1).Send(
            Arg.Is<WithdrawNotFoundTargetCommand>(command =>
                command.Target.Kind == "edition" && command.Target.Reference == Isbn &&
                command.QuantityFound == 1 && command.Reason == NotFoundWithdrawalReason.Damaged &&
                command.Note == "Retiré après vérification" && command.By == UserId.Create(VolunteerId)),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task PostEditionWithdrawal_ForRareBooksOnlyUser_Returns403()
    {
        await using var application = CreateApplication();
        var endpoint = FindEndpoint(
            application,
            "POST",
            "/books/admin/not-found-reports/editions/{isbn13}/withdrawal");

        var statusCode = await AuthorizeEndpointAsync(application, endpoint, Principal("LivresRares"));

        statusCode.Should().Be((int)HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task PostRareWithdrawal_ForRareBooksUser_Returns200()
    {
        var mediator = Substitute.For<IMediator>();
        mediator.Send(Arg.Any<WithdrawNotFoundTargetCommand>(), Arg.Any<CancellationToken>())
            .Returns(new NotFoundClosureResult(1, null, null, null));
        await using var application = CreateApplication(mediator);
        var endpoint = FindEndpoint(
            application,
            "POST",
            "/books/admin/not-found-reports/rare-books/{rareBookId:guid}/withdrawal");

        var response = await InvokeAsync(
            application,
            endpoint,
            "POST",
            "{\"reason\":\"NotFoundOnShelf\",\"note\":\"Fiche retirée\"}",
            Principal("LivresRares"),
            new Dictionary<string, object?> { ["rareBookId"] = RareBookId.ToString() });

        response.Response.StatusCode.Should().Be((int)HttpStatusCode.OK);
        await mediator.Received(1).Send(
            Arg.Is<WithdrawNotFoundTargetCommand>(command =>
                command.Target.Kind == "rare" && command.Target.Reference == RareBookId.ToString() &&
                command.QuantityFound == 0 && command.Reason == NotFoundWithdrawalReason.NotFoundOnShelf &&
                command.Note == "Fiche retirée" && command.By == UserId.Create(VolunteerId)),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task PostFound_WhenNothingToClose_Returns409()
    {
        var mediator = Substitute.For<IMediator>();
        mediator.Send(Arg.Any<MarkNotFoundTargetFoundCommand>(), Arg.Any<CancellationToken>())
            .Returns(Vole_Papillon_Damour.Domain.Common.Errors.Errors.NotFoundReport.NothingToClose());
        await using var application = CreateApplication(mediator);
        var endpoint = FindEndpoint(
            application,
            "POST",
            "/books/admin/not-found-reports/editions/{isbn13}/found");

        var response = await InvokeAsync(
            application,
            endpoint,
            "POST",
            "{\"note\":\"Aucun signalement ouvert\"}",
            Principal("Administration"),
            new Dictionary<string, object?> { ["isbn13"] = Isbn });

        response.Response.StatusCode.Should().Be((int)HttpStatusCode.Conflict);
    }

    private static WebApplication CreateApplication(IMediator? mediator = null)
    {
        var builder = WebApplication.CreateBuilder();
        builder.Services.AddAuthorization(options =>
        {
            options.AddPolicy("Administration", policy => policy.RequireRole("Administration", "Admin"));
            options.AddPolicy("RareBooks", policy =>
                policy.RequireRole("LivresRares", "Administration", "Admin"));
        });
        builder.Services.AddSingleton(mediator ?? Substitute.For<IMediator>());
        builder.Services.AddSingleton(Substitute.For<IMapper>());
        builder.Services.AddSingleton(Substitute.For<ISSEClientManager>());
        builder.Services.AddSingleton(Substitute.For<IAccountDeletionService>());

        var application = builder.Build();
        application.UseRouting();
        application.UseNotFoundReportAdministrationController();
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
        policyName.Should().NotBeNullOrWhiteSpace();
        var authorization = application.Services.GetRequiredService<IAuthorizationService>();
        var result = await authorization.AuthorizeAsync(principal, resource: null, policyName!);
        return result.Succeeded
            ? (int)HttpStatusCode.OK
            : principal.Identity?.IsAuthenticated == true
                ? (int)HttpStatusCode.Forbidden
                : (int)HttpStatusCode.Unauthorized;
    }

    private sealed class TestRequestBodyDetectionFeature : IHttpRequestBodyDetectionFeature
    {
        public bool CanHaveBody => true;
    }

    private static ClaimsPrincipal Principal(string role) => new(
        new ClaimsIdentity(
        [
            new Claim("oid", VolunteerId.ToString()),
            new Claim(ClaimTypes.Role, role)
        ],
        authenticationType: "Test"));
}
