using System.Net;
using System.Security.Claims;
using System.Text;
using System.Text.Json.Nodes;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using FluentAssertions;
using MapsterMapper;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NSubstitute;
using Vole_Papillon_Damour.Api.Controllers;
using Vole_Papillon_Damour.Application.Common.Interfaces.Services;
using Vole_Papillon_Damour.Application.MemberCards.Queries.GetMyCard;
using Vole_Papillon_Damour.Application.MemberCards.Queries.ResolveMemberCard;
using Vole_Papillon_Damour.Application.Purchases.Queries.GetMyPurchases;

namespace Vole_Papillon_Damour.Api.tests.MemberAccount;

public sealed class MemberCardEndpointsTests
{
    private static readonly Guid ExternalId = Guid.Parse("7f0b0f0e-0000-0000-0000-00000000c0de");
    private static readonly DateTimeOffset IssuedAt = new(2026, 9, 23, 10, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Resolve_WithoutCaisseRole_Returns403BeforeCallingMediator()
    {
        var mediator = Substitute.For<IMediator>();
        await using var application = CreateApplication(mediator);
        var endpoint = FindEndpoint(application, "POST", "/scan/member-cards/resolve");

        endpoint.Should().NotBeNull();
        endpoint!.Metadata.GetOrderedMetadata<Microsoft.AspNetCore.Authorization.IAuthorizeData>()
            .Should().Contain(data => data.Policy == "Caisse");

        var response = await InvokeWithAuthorizationAsync(
            application,
            endpoint,
            "POST",
            "{\"credential\":\"VPDC1.AAAA.BBBB\"}",
            AuthenticatedPrincipal(withCaisse: false));

        response.Response.StatusCode.Should().Be((int)HttpStatusCode.Forbidden);
        await mediator.DidNotReceive().Send(Arg.Any<ResolveMemberCardQuery>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Resolve_WithCaisseRole_ReturnsOnlyDisplayLabel()
    {
        var mediator = Substitute.For<IMediator>();
        mediator.Send(Arg.Any<ResolveMemberCardQuery>(), Arg.Any<CancellationToken>())
            .Returns(new MemberCardConfirmation("Camille"));
        await using var application = CreateApplication(mediator);
        var endpoint = FindEndpoint(application, "POST", "/scan/member-cards/resolve");

        var response = await InvokeAsync(
            application,
            endpoint,
            "POST",
            "{\"credential\":\"VPDC1.AAAA.BBBB\"}",
            AuthenticatedPrincipal(withCaisse: true));
        var json = JsonNode.Parse(await ReadBodyAsync(response))!.AsObject();

        response.Response.StatusCode.Should().Be((int)HttpStatusCode.OK);
        json.Count.Should().Be(1);
        json["displayLabel"]!.GetValue<string>().Should().Be("Camille");
        await mediator.Received(1).Send(
            Arg.Is<ResolveMemberCardQuery>(query => query.Credential == "VPDC1.AAAA.BBBB"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GetMyCard_SetsNoStoreCacheControl()
    {
        var mediator = Substitute.For<IMediator>();
        mediator.Send(Arg.Any<GetMyCardQuery>(), Arg.Any<CancellationToken>())
            .Returns(new MyCardResult("VPDC1.AAAA.BBBB", "LUNE-4271", "Camille", IssuedAt));
        await using var application = CreateApplication(mediator);
        var endpoint = FindEndpoint(application, "GET", "/catalog/me/card");

        var response = await InvokeAsync(application, endpoint, "GET", principal: AuthenticatedPrincipal());

        response.Response.StatusCode.Should().Be((int)HttpStatusCode.OK);
        response.Response.Headers.CacheControl.ToString().Should().Be("no-store");
    }

    [Fact]
    public async Task GetMyPurchases_ForwardsCursorAndLimitWithoutPriceFields()
    {
        var mediator = Substitute.For<IMediator>();
        mediator.Send(Arg.Any<GetMyPurchasesQuery>(), Arg.Any<CancellationToken>())
            .Returns(new MyPurchasesPage(
                [new PurchasePassageResult(
                    Guid.Parse("a3d29d66-d784-4c02-95cb-e196c8d881c7"),
                    "A3D29D66",
                    IssuedAt,
                    null,
                    null,
                    1,
                    [new PurchaseLineResult(
                        Guid.NewGuid(), "edition", "9782070612758", null, "Le Petit Prince",
                        "Antoine de Saint-Exupéry", "Gallimard", 1946, "Broché", 1, "Associated", null)])],
                "opaque-cursor"));
        await using var application = CreateApplication(mediator);
        var endpoint = FindEndpoint(application, "GET", "/catalog/me/purchases");

        var response = await InvokeAsync(
            application,
            endpoint,
            "GET",
            principal: AuthenticatedPrincipal(),
            queryString: "?cursor=older-page&limit=3");
        var json = JsonNode.Parse(await ReadBodyAsync(response))!.AsObject();
        var jsonText = json.ToJsonString();

        response.Response.StatusCode.Should().Be((int)HttpStatusCode.OK);
        response.Response.Headers.CacheControl.ToString().Should().Be("no-store");
        jsonText.ToLowerInvariant().Should().NotContain("price");
        jsonText.ToLowerInvariant().Should().NotContain("amount");
        jsonText.ToLowerInvariant().Should().NotContain("total");
        json["passages"]![0]!["reference"]!.GetValue<string>().Should().Be("A3D29D66");
        await mediator.Received(1).Send(
            Arg.Is<GetMyPurchasesQuery>(query =>
                query.ExternalId == ExternalId.ToString("D") &&
                query.Email == "camille@example.test" &&
                query.FirstName == "Camille" &&
                query.LastName == "Durand" &&
                query.Cursor == "older-page" &&
                query.Limit == 3),
            Arg.Any<CancellationToken>());
    }

    private static WebApplication CreateApplication(IMediator? mediator = null)
    {
        var builder = WebApplication.CreateBuilder();
        builder.Services.AddAuthentication("MemberCardTest")
            .AddScheme<AuthenticationSchemeOptions, MemberCardTestAuthenticationHandler>("MemberCardTest", _ => { });
        builder.Services.AddAuthorization(options =>
            options.AddPolicy("Caisse", policy => policy.RequireRole("Caisse")));
        builder.Services.AddSingleton(mediator ?? Substitute.For<IMediator>());
        builder.Services.AddSingleton(Substitute.For<IMapper>());
        builder.Services.AddSingleton(Substitute.For<ISSEClientManager>());
        builder.Services.AddSingleton(Substitute.For<IAccountDeletionService>());

        var application = builder.Build();
        application.UseRouting();
        application.UseAuthentication();
        application.UseAuthorization();
        application.UseBookController();
        application.UseMemberAccountController();
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
        string? body = null,
        ClaimsPrincipal? principal = null,
        string? queryString = null)
    {
        var context = new DefaultHttpContext
        {
            RequestServices = application.Services,
            User = principal ?? AuthenticatedPrincipal()
        };
        context.Request.Method = method;
        context.Request.ContentType = "application/json";
        if (queryString is not null)
        {
            context.Request.QueryString = new QueryString(queryString);
        }
        var bytes = Encoding.UTF8.GetBytes(body ?? string.Empty);
        context.Request.Body = new MemoryStream(bytes);
        context.Request.ContentLength = bytes.Length;
        context.Features.Set<IHttpRequestBodyDetectionFeature>(new TestRequestBodyDetectionFeature());
        context.Response.Body = new MemoryStream();
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
        ClaimsPrincipal principal)
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
        context.SetEndpoint(endpoint);

        var middleware = new AuthorizationMiddleware(
            _ => endpoint.RequestDelegate!(context),
            application.Services.GetRequiredService<IAuthorizationPolicyProvider>());
        await middleware.Invoke(context);
        return context;
    }

    private static async Task<string> ReadBodyAsync(DefaultHttpContext context)
    {
        context.Response.Body.Position = 0;
        using var reader = new StreamReader(context.Response.Body, Encoding.UTF8, leaveOpen: true);
        return await reader.ReadToEndAsync();
    }

    private static ClaimsPrincipal AuthenticatedPrincipal(bool withCaisse = false)
    {
        var claims = new List<Claim>
        {
            new("oid", ExternalId.ToString()),
            new("email", "camille@example.test"),
            new("given_name", "Camille"),
            new("family_name", "Durand")
        };
        if (withCaisse)
        {
            claims.Add(new Claim(ClaimTypes.Role, "Caisse"));
        }

        return new ClaimsPrincipal(new ClaimsIdentity(claims, "Test"));
    }

    private sealed class TestRequestBodyDetectionFeature : IHttpRequestBodyDetectionFeature
    {
        public bool CanHaveBody => true;
    }

    private sealed class MemberCardTestAuthenticationHandler(
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
