using System.Net;
using System.Security.Claims;
using System.Text;
using System.Text.Json.Nodes;
using System.Text.Encodings.Web;
using ErrorOr;
using FluentAssertions;
using MediatR;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NSubstitute;
using Vole_Papillon_Damour.Api.Controllers;
using Vole_Papillon_Damour.Application.Books.Commands.RegisterSale;
using Vole_Papillon_Damour.Application.Books.Common;
using Vole_Papillon_Damour.Application.CheckoutPassages.Commands.AssociateCheckoutPassage;
using Vole_Papillon_Damour.Application.CheckoutPassages.Commands.DissociateCheckoutPassage;
using Vole_Papillon_Damour.Application.CheckoutPassages.Queries.LookupCheckoutPassage;
using Vole_Papillon_Damour.Domain.BookMovementAggregate.ValueObjects;

namespace Vole_Papillon_Damour.Api.tests.CheckoutPassages;

public sealed class CheckoutPassageEndpointsTests
{
    private static readonly Guid VolunteerId = Guid.Parse("ac2a8e70-53ec-4df6-8a13-f93f9b74b250");
    private static readonly Guid PassageId = Guid.Parse("a3d29d66-d784-4c02-95cb-e196c8d881c7");

    [Fact]
    public async Task PutMember_WithoutCaisseRole_Returns403()
    {
        var mediator = Substitute.For<IMediator>();
        await using var application = CreateApplication(mediator);
        var endpoint = FindEndpoint(application, "PUT", "/scan/passages/{checkoutPassageId:guid}/member");

        endpoint.Should().NotBeNull();
        var response = await InvokeWithAuthorizationAsync(
            application,
            endpoint!,
            "PUT",
            "{\"credential\":\"VPDC1.AAAA.BBBB\",\"occurredAt\":\"2026-09-24T12:30:00+00:00\"}",
            AuthenticatedPrincipal(withCaisse: false),
            new Dictionary<string, object?> { ["checkoutPassageId"] = PassageId.ToString() });

        response.Response.StatusCode.Should().Be((int)HttpStatusCode.Forbidden);
        await mediator.DidNotReceive().Send(
            Arg.Any<AssociateCheckoutPassageCommand>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task PutMember_ForwardsPassageCredentialAndVolunteer()
    {
        var mediator = Substitute.For<IMediator>();
        var occurredAt = new DateTimeOffset(2026, 9, 24, 14, 30, 0, TimeSpan.FromHours(2));
        mediator.Send(Arg.Any<AssociateCheckoutPassageCommand>(), Arg.Any<CancellationToken>())
            .Returns(new CheckoutPassageAssociationResult(PassageId, "Associated", "Camille", false));
        await using var application = CreateApplication(mediator);
        var endpoint = FindEndpoint(application, "PUT", "/scan/passages/{checkoutPassageId:guid}/member");

        var response = await InvokeAsync(
            application,
            endpoint,
            "PUT",
            "{\"credential\":\"VPDC1.AAAA.BBBB\",\"occurredAt\":\"2026-09-24T14:30:00+02:00\"}",
            AuthenticatedPrincipal(withCaisse: true),
            new Dictionary<string, object?> { ["checkoutPassageId"] = PassageId.ToString() });
        var json = JsonNode.Parse(await ReadBodyAsync(response))!.AsObject();

        response.Response.StatusCode.Should().Be((int)HttpStatusCode.OK);
        json["checkoutPassageId"]!.GetValue<Guid>().Should().Be(PassageId);
        json["status"]!.GetValue<string>().Should().Be("Associated");
        json["displayLabel"]!.GetValue<string>().Should().Be("Camille");
        json["alreadyProcessed"]!.GetValue<bool>().Should().BeFalse();
        await mediator.Received(1).Send(
            Arg.Is<AssociateCheckoutPassageCommand>(command =>
                command.CheckoutPassageId == PassageId &&
                command.Credential == "VPDC1.AAAA.BBBB" &&
                command.OccurredAt == occurredAt.UtcDateTime &&
                command.VolunteerId.Value == VolunteerId),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task PostDissociate_RequiresAdministrationRoleAndForwardsAuditReason()
    {
        var mediator = Substitute.For<IMediator>();
        mediator.Send(Arg.Any<DissociateCheckoutPassageCommand>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success);
        await using var application = CreateApplication(mediator);
        var endpoint = FindEndpoint(application, "POST", "/administration/checkout-passages/{id:guid}/dissociate");

        var forbidden = await InvokeWithAuthorizationAsync(
            application,
            endpoint!,
            "POST",
            "{\"reason\":\"Correction demandée\"}",
            AuthenticatedPrincipal(withCaisse: true),
            new Dictionary<string, object?> { ["id"] = PassageId.ToString() });
        forbidden.Response.StatusCode.Should().Be((int)HttpStatusCode.Forbidden);

        var response = await InvokeWithAuthorizationAsync(
            application,
            endpoint!,
            "POST",
            "{\"reason\":\"Correction demandée\"}",
            AuthenticatedPrincipal(withCaisse: false, withAdministration: true),
            new Dictionary<string, object?> { ["id"] = PassageId.ToString() });

        response.Response.StatusCode.Should().Be((int)HttpStatusCode.NoContent);
        await mediator.Received(1).Send(
            Arg.Is<DissociateCheckoutPassageCommand>(command =>
                command.CheckoutPassageId == PassageId &&
                command.AdministratorId.Value == VolunteerId &&
                command.Reason == "Correction demandée"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GetPassageLookup_WithAmbiguousReferenceReturns409()
    {
        var mediator = Substitute.For<IMediator>();
        mediator.Send(Arg.Any<LookupCheckoutPassageQuery>(), Arg.Any<CancellationToken>())
            .Returns(Error.Conflict("CheckoutPassage.AmbiguousReference", "Use the full identifier."));
        await using var application = CreateApplication(mediator);
        var endpoint = FindEndpoint(application, "GET", "/administration/checkout-passages/lookup");
        var context = CreateContext(
            application,
            "GET",
            string.Empty,
            AuthenticatedPrincipal(withCaisse: false, withAdministration: true),
            null);
        context.Request.QueryString = new QueryString("?reference=3F2A9C1B");
        context.SetEndpoint(endpoint!);

        await endpoint!.RequestDelegate!(context);

        context.Response.StatusCode.Should().Be((int)HttpStatusCode.Conflict);
        await mediator.Received(1).Send(
            Arg.Is<LookupCheckoutPassageQuery>(query => query.Reference == "3F2A9C1B"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task PostSale_WithoutPassageField_SendsNullPassage()
    {
        var mediator = Substitute.For<IMediator>();
        SetupSaleResult(mediator);
        await using var application = CreateApplication(mediator);
        var endpoint = FindEndpoint(application, "POST", "/scan/sales");

        var response = await InvokeAsync(
            application,
            endpoint,
            "POST",
            "{\"isbn\":\"9782010000001\",\"quantity\":1,\"occurredAt\":\"2026-09-24T12:30:00Z\",\"clientGestureId\":\"e1d4251a-90c3-47c4-9185-4d0c4a3c4f53\"}",
            AuthenticatedPrincipal(withCaisse: true));

        response.Response.StatusCode.Should().Be((int)HttpStatusCode.OK);
        await mediator.Received(1).Send(
            Arg.Is<RegisterSaleCommand>(command => command.CheckoutPassageId == null),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task PostSale_WithEmptyGuidPassage_SendsNullPassage()
    {
        var mediator = Substitute.For<IMediator>();
        SetupSaleResult(mediator);
        await using var application = CreateApplication(mediator);
        var endpoint = FindEndpoint(application, "POST", "/scan/sales");

        var response = await InvokeAsync(
            application,
            endpoint,
            "POST",
            "{\"isbn\":\"9782010000001\",\"quantity\":1,\"occurredAt\":\"2026-09-24T12:30:00Z\",\"clientGestureId\":\"e1d4251a-90c3-47c4-9185-4d0c4a3c4f53\",\"checkoutPassageId\":\"00000000-0000-0000-0000-000000000000\"}",
            AuthenticatedPrincipal(withCaisse: true));

        response.Response.StatusCode.Should().Be((int)HttpStatusCode.OK);
        await mediator.Received(1).Send(
            Arg.Is<RegisterSaleCommand>(command => command.CheckoutPassageId == null),
            Arg.Any<CancellationToken>());
    }

    private static void SetupSaleResult(IMediator mediator)
    {
        var result = new RegisterSaleResult(
            "9782010000001",
            BookMovementId.Create(Guid.Parse("37b4c19e-9ee2-49be-8705-6db6ebf2c5f0")),
            1,
            1,
            1,
            null,
            SaleFairMatchStatus.NoOpenFair,
            HadNoAvailableStock: false,
            HadUnreleasedAnnouncement: false,
            IsRare: false,
            ClockSuspect: false,
            AlreadyProcessed: false);
        mediator.Send(Arg.Any<RegisterSaleCommand>(), Arg.Any<CancellationToken>())
            .Returns((ErrorOr<RegisterSaleResult>)result);
    }

    private static WebApplication CreateApplication(IMediator mediator)
    {
        var builder = WebApplication.CreateBuilder();
        builder.Services.AddAuthentication("CheckoutPassageTest")
            .AddScheme<AuthenticationSchemeOptions, TestAuthenticationHandler>("CheckoutPassageTest", _ => { });
        builder.Services.AddAuthorization(options =>
        {
            options.AddPolicy("Caisse", policy => policy.RequireRole("Caisse"));
            options.AddPolicy("Administration", policy => policy.RequireRole("Administration"));
        });
        builder.Services.AddSingleton(mediator);

        var application = builder.Build();
        application.UseRouting();
        application.UseAuthentication();
        application.UseAuthorization();
        application.UseBookController();
        application.UseCheckoutPassageController();
        return application;
    }

    private static RouteEndpoint? FindEndpoint(WebApplication application, string method, string route) =>
        application.Services.GetRequiredService<EndpointDataSource>()
            .Endpoints.OfType<RouteEndpoint>()
            .SingleOrDefault(endpoint => endpoint.RoutePattern.RawText == route &&
                endpoint.Metadata.GetMetadata<HttpMethodMetadata>()?.HttpMethods.Contains(method) == true);

    private static async Task<DefaultHttpContext> InvokeAsync(
        WebApplication application,
        RouteEndpoint? endpoint,
        string method,
        string body,
        ClaimsPrincipal principal,
        IDictionary<string, object?>? routeValues = null)
    {
        var context = CreateContext(application, method, body, principal, routeValues);
        if (endpoint is null)
        {
            context.Response.StatusCode = (int)HttpStatusCode.NotFound;
            return context;
        }

        context.SetEndpoint(endpoint);
        await endpoint.RequestDelegate!(context);
        return context;
    }

    private static async Task<DefaultHttpContext> InvokeWithAuthorizationAsync(
        WebApplication application,
        RouteEndpoint endpoint,
        string method,
        string body,
        ClaimsPrincipal principal,
        IDictionary<string, object?>? routeValues = null)
    {
        var context = CreateContext(application, method, body, principal, routeValues);
        context.SetEndpoint(endpoint);
        var middleware = new AuthorizationMiddleware(
            _ => endpoint.RequestDelegate!(context),
            application.Services.GetRequiredService<IAuthorizationPolicyProvider>());
        await middleware.Invoke(context);
        return context;
    }

    private static DefaultHttpContext CreateContext(
        WebApplication application,
        string method,
        string body,
        ClaimsPrincipal principal,
        IDictionary<string, object?>? routeValues)
    {
        var context = new DefaultHttpContext
        {
            RequestServices = application.Services,
            User = principal
        };
        context.Request.Method = method;
        context.Request.ContentType = "application/json";
        var bytes = Encoding.UTF8.GetBytes(body);
        context.Request.Body = new MemoryStream(bytes);
        context.Request.ContentLength = bytes.Length;
        context.Features.Set<IHttpRequestBodyDetectionFeature>(new TestRequestBodyDetectionFeature());
        context.Response.Body = new MemoryStream();
        if (routeValues is not null)
        {
            foreach (var (key, value) in routeValues)
            {
                context.Request.RouteValues[key] = value;
            }
        }

        return context;
    }

    private static async Task<string> ReadBodyAsync(DefaultHttpContext context)
    {
        context.Response.Body.Position = 0;
        using var reader = new StreamReader(context.Response.Body, Encoding.UTF8, leaveOpen: true);
        return await reader.ReadToEndAsync();
    }

    private static ClaimsPrincipal AuthenticatedPrincipal(bool withCaisse, bool withAdministration = false) =>
        new(new ClaimsIdentity(
        [
            new Claim("oid", VolunteerId.ToString()),
            new Claim(ClaimTypes.Role, withAdministration ? "Administration" : withCaisse ? "Caisse" : "Volunteer")
        ],
        "Test"));

    private sealed class TestRequestBodyDetectionFeature : IHttpRequestBodyDetectionFeature
    {
        public bool CanHaveBody => true;
    }

    private sealed class TestAuthenticationHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder)
        : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
    {
        protected override Task<AuthenticateResult> HandleAuthenticateAsync() =>
            Task.FromResult(AuthenticateResult.NoResult());

        protected override Task HandleForbiddenAsync(AuthenticationProperties properties)
        {
            Response.StatusCode = StatusCodes.Status403Forbidden;
            return Task.CompletedTask;
        }

        protected override Task HandleChallengeAsync(AuthenticationProperties properties)
        {
            Response.StatusCode = StatusCodes.Status401Unauthorized;
            return Task.CompletedTask;
        }
    }
}
