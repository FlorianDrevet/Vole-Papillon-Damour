using System.Net;
using System.Security.Claims;
using ErrorOr;
using FluentAssertions;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using Vole_Papillon_Damour.Api.Controllers;
using Vole_Papillon_Damour.Application.Common.Interfaces.Services;
using Vole_Papillon_Damour.Application.Common.Models;
using Vole_Papillon_Damour.Application.FairDigest.Commands.ResumeFairDigest;
using Vole_Papillon_Damour.Application.FairDigest.Commands.SendFairDigestTest;
using Vole_Papillon_Damour.Application.FairDigest.Commands.SuspendFairDigest;
using Vole_Papillon_Damour.Application.FairDigest.Common;
using Vole_Papillon_Damour.Application.FairDigest.Queries.GetFairDigestStatus;
using Vole_Papillon_Damour.Application.FairDigest.Queries.PreviewFairDigest;
using Vole_Papillon_Damour.Domain.EventsAggregate.ValueObjects;
using Vole_Papillon_Damour.Domain.UserAggregate.ValueObjects;

namespace Vole_Papillon_Damour.Api.tests.FairDigest;

public sealed class FairDigestAdministrationEndpointsTests
{
    private static readonly Guid AdministratorId = Guid.Parse("7f0b0f0e-0000-0000-0000-00000000c0de");
    private static readonly Guid CampaignId = Guid.Parse("a83c8586-6c92-40a4-a3ab-0f74515b20d7");

    [Fact]
    public async Task FairDigestAdministrationRoutes_RequireAdministrationRole()
    {
        await using var application = CreateApplication(Substitute.For<IMediator>());
        var routes = new[]
        {
            ("GET", "/books/admin/fair-digest"),
            ("GET", "/books/admin/fair-digest/preview"),
            ("POST", "/books/admin/fair-digest/test"),
            ("POST", "/books/admin/fair-digest/suspend"),
            ("POST", "/books/admin/fair-digest/resume")
        };

        foreach (var (method, route) in routes)
        {
            var endpoint = FindEndpoint(application, method, route);
            endpoint.Metadata.GetOrderedMetadata<IAuthorizeData>()
                .Should().ContainSingle(data => data.Policy == "Administration");
        }
    }

    [Fact]
    public async Task GetStatus_RejectsAMemberBeforeCallingMediator()
    {
        var mediator = Substitute.For<IMediator>();
        await using var application = CreateApplication(mediator);
        var endpoint = FindEndpoint(application, "GET", "/books/admin/fair-digest");

        var status = await AuthorizeEndpointAsync(application, endpoint, Principal("Member"));

        status.Should().Be((int)HttpStatusCode.Forbidden);
        await mediator.DidNotReceive().Send(Arg.Any<GetFairDigestStatusQuery>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GetStatus_ForwardsTheStatusQuery()
    {
        var mediator = Substitute.For<IMediator>();
        mediator.Send(Arg.Any<GetFairDigestStatusQuery>(), Arg.Any<CancellationToken>())
            .Returns(new FairDigestStatusResult(
                true,
                CampaignId,
                "Bourse de printemps",
                DateTimeOffset.Parse("2027-03-13T09:00:00Z"),
                CampaignId,
                DateTimeOffset.Parse("2027-03-10T09:00:00Z"),
                false,
                12,
                null));
        await using var application = CreateApplication(mediator);
        var endpoint = FindEndpoint(application, "GET", "/books/admin/fair-digest");

        var response = await InvokeAsync(application, endpoint, "GET", Principal("Administration"));

        response.Response.StatusCode.Should().Be((int)HttpStatusCode.OK);
        await mediator.Received(1).Send(Arg.Any<GetFairDigestStatusQuery>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GetPreview_RendersTheAuthenticatedAdministratorsPreviewAsHtml()
    {
        var mediator = Substitute.For<IMediator>();
        mediator.Send(Arg.Any<PreviewFairDigestQuery>(), Arg.Any<CancellationToken>())
            .Returns(ErrorOrFactory.From(new FairDigestPreviewResult(
                CampaignId,
                Fair(),
                new FairDigestMemberContent([], [], SkippedNoContent: false),
                IsExample: true)));
        var tokenService = Substitute.For<IUnsubscribeTokenService>();
        tokenService.Create(AdministratorId, UnsubscribePurpose.FairDigest).Returns("preview-token");
        var renderer = Substitute.For<IFairDigestEmailRenderer>();
        renderer.Render(Arg.Any<FairDigestEmailMessage>(), "preview-token")
            .Returns(new FairDigestEmailContent("Sujet", "Aperçu", "Texte", "<main>preview</main>"));
        await using var application = CreateApplication(mediator, tokenService, renderer);
        var endpoint = FindEndpoint(application, "GET", "/books/admin/fair-digest/preview");

        var response = await InvokeAsync(application, endpoint, "GET", Principal("Administration"));

        response.Response.StatusCode.Should().Be((int)HttpStatusCode.OK);
        response.Response.ContentType.Should().StartWith("text/html");
        await mediator.Received(1).Send(
            Arg.Is<PreviewFairDigestQuery>(query => query.AdministratorId == UserId.Create(AdministratorId)),
            Arg.Any<CancellationToken>());
        renderer.Received(1).Render(
            Arg.Is<FairDigestEmailMessage>(message =>
                message.MemberId == UserId.Create(AdministratorId) && message.Email == "preview@example.test"),
            "preview-token");
    }

    [Fact]
    public async Task SuspendAndResume_ForwardTheAuthenticatedAdministrator()
    {
        var mediator = Substitute.For<IMediator>();
        mediator.Send(Arg.Any<SuspendFairDigestCommand>(), Arg.Any<CancellationToken>())
            .Returns(new FairDigestCampaignOperationResult(CampaignId, Changed: true, DateTimeOffset.UtcNow));
        mediator.Send(Arg.Any<ResumeFairDigestCommand>(), Arg.Any<CancellationToken>())
            .Returns(new FairDigestCampaignOperationResult(CampaignId, Changed: true, null));
        await using var application = CreateApplication(mediator);
        var suspend = FindEndpoint(application, "POST", "/books/admin/fair-digest/suspend");
        var resume = FindEndpoint(application, "POST", "/books/admin/fair-digest/resume");

        var suspended = await InvokeAsync(application, suspend, "POST", Principal("Administration"));
        var resumed = await InvokeAsync(application, resume, "POST", Principal("Administration"));

        suspended.Response.StatusCode.Should().Be((int)HttpStatusCode.OK);
        resumed.Response.StatusCode.Should().Be((int)HttpStatusCode.OK);
        await mediator.Received(1).Send(
            Arg.Is<SuspendFairDigestCommand>(command => command.AdministratorId == UserId.Create(AdministratorId)),
            Arg.Any<CancellationToken>());
        await mediator.Received(1).Send(
            Arg.Is<ResumeFairDigestCommand>(command => command.AdministratorId == UserId.Create(AdministratorId)),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SendTest_UsesOnlyTheAuthenticatedAdministratorsIdentity()
    {
        var mediator = Substitute.For<IMediator>();
        mediator.Send(Arg.Any<SendFairDigestTestCommand>(), Arg.Any<CancellationToken>())
            .Returns(new FairDigestTestSendResult("admin@example.test", IsExample: true));
        await using var application = CreateApplication(mediator);
        var endpoint = FindEndpoint(application, "POST", "/books/admin/fair-digest/test");

        var response = await InvokeAsync(application, endpoint, "POST", Principal("Administration"));

        response.Response.StatusCode.Should().Be((int)HttpStatusCode.OK);
        await mediator.Received(1).Send(
            Arg.Is<SendFairDigestTestCommand>(command => command.AdministratorId == UserId.Create(AdministratorId)),
            Arg.Any<CancellationToken>());
    }

    private static WebApplication CreateApplication(
        IMediator mediator,
        IUnsubscribeTokenService? tokenService = null,
        IFairDigestEmailRenderer? renderer = null)
    {
        var builder = WebApplication.CreateBuilder();
        builder.Services.AddAuthorization(options =>
            options.AddPolicy("Administration", policy => policy.RequireRole("Administration", "Admin")));
        builder.Services.AddSingleton(mediator);
        builder.Services.AddSingleton(tokenService ?? Substitute.For<IUnsubscribeTokenService>());
        builder.Services.AddSingleton(renderer ?? Substitute.For<IFairDigestEmailRenderer>());

        var application = builder.Build();
        application.UseRouting();
        application.UseBookAdministrationController();
        return application;
    }

    private static RouteEndpoint FindEndpoint(WebApplication application, string method, string route) =>
        application.Services.GetRequiredService<EndpointDataSource>()
            .Endpoints.OfType<RouteEndpoint>()
            .Single(endpoint =>
                endpoint.RoutePattern.RawText == route &&
                endpoint.Metadata.GetMetadata<HttpMethodMetadata>()?.HttpMethods.Contains(method) == true);

    private static async Task<DefaultHttpContext> InvokeAsync(
        WebApplication application,
        RouteEndpoint endpoint,
        string method,
        ClaimsPrincipal principal)
    {
        var context = new DefaultHttpContext
        {
            RequestServices = application.Services,
            User = principal
        };
        context.Request.Method = method;
        context.Response.Body = new MemoryStream();
        context.SetEndpoint(endpoint);
        await endpoint.RequestDelegate!(context);
        context.Response.Body.Position = 0;
        return context;
    }

    private static async Task<int> AuthorizeEndpointAsync(
        WebApplication application,
        RouteEndpoint endpoint,
        ClaimsPrincipal principal)
    {
        var policyName = endpoint.Metadata.GetOrderedMetadata<IAuthorizeData>()
            .Single().Policy;
        var result = await application.Services.GetRequiredService<IAuthorizationService>()
            .AuthorizeAsync(principal, resource: null, policyName!);
        return result.Succeeded
            ? (int)HttpStatusCode.OK
            : principal.Identity?.IsAuthenticated == true
                ? (int)HttpStatusCode.Forbidden
                : (int)HttpStatusCode.Unauthorized;
    }

    private static ClaimsPrincipal Principal(string role) => new(
        new ClaimsIdentity(
            [
                new Claim("oid", AdministratorId.ToString("D")),
                new Claim("email", "preview@example.test"),
                new Claim("given_name", "Marie"),
                new Claim(ClaimTypes.Role, role)
            ],
            authenticationType: "Test"));

    private static FairDigestEvent Fair() => new(
        CampaignId,
        EventsType.EventsTypeEnum.Books,
        DateTimeOffset.Parse("2027-03-13T09:00:00Z"),
        DateTimeOffset.Parse("2027-03-13T18:00:00Z"),
        false);
}
