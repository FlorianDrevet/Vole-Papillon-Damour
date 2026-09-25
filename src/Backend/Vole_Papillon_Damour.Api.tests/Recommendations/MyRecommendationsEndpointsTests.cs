using System.Net;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using ErrorOr;
using FluentAssertions;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using Vole_Papillon_Damour.Api.Controllers;
using Vole_Papillon_Damour.Application.Books.Common;
using Vole_Papillon_Damour.Application.Recommendations.Commands.SetRecommendationPreference;
using Vole_Papillon_Damour.Application.Recommendations.Queries.GetMyRecommendations;
using Vole_Papillon_Damour.Application.Recommendations.Queries.GetRecommendationPreference;
using Vole_Papillon_Damour.Domain.RecommendationAggregate.ValueObjects;

namespace Vole_Papillon_Damour.Api.tests.Recommendations;

public sealed class MyRecommendationsEndpointsTests
{
    private const string Isbn13 = "9782070408504";

    [Fact]
    public async Task GetRecommendations_ReturnsFlattenedItemsNoStoreAndRequiresAuthorization()
    {
        var mediator = CreateMediator();
        var item = new PersonalRecommendation(
            new PublicCatalogBookResult(
                Isbn13,
                "Dune",
                "Frank Herbert",
                "Robert Laffont",
                1965,
                "Poche",
                "fr",
                "Science-fiction",
                null,
                null,
                1,
                0,
                null,
                null,
                DateTimeOffset.Parse("2026-09-01T00:00:00Z"),
                DateTimeOffset.Parse("2026-09-02T00:00:00Z"),
                false),
            NeighborReason.NextTome,
            "Fondation");
        mediator.Send(Arg.Any<GetMyRecommendationsQuery>(), Arg.Any<CancellationToken>())
            .Returns(ErrorOrFactory.From(new MyRecommendationsResult(RecommendationStatus.Enabled, [item])));
        await using var application = CreateApplication(mediator);
        var endpoint = FindEndpoint(application, "/catalog/me/recommendations", "GET");

        var context = await InvokeAsync(application, endpoint, "GET");

        context.Response.StatusCode.Should().Be((int)HttpStatusCode.OK);
        context.Response.Headers.CacheControl.ToString().Should().Be("no-store");
        endpoint.Metadata.GetOrderedMetadata<Microsoft.AspNetCore.Authorization.IAuthorizeData>()
            .Should().NotBeEmpty();
        using var document = await JsonDocument.ParseAsync(context.Response.Body);
        document.RootElement.GetProperty("status").GetString().Should().Be("Enabled");
        var recommendation = document.RootElement.GetProperty("items")[0];
        recommendation.GetProperty("isbn13").GetString().Should().Be(Isbn13);
        recommendation.GetProperty("reason").GetString().Should().Be("NextTome");
        recommendation.GetProperty("seedTitle").GetString().Should().Be("Fondation");
        recommendation.TryGetProperty("book", out _).Should().BeFalse();
    }

    [Fact]
    public async Task GetPreference_ReturnsPreferenceNoStoreAndRequiresAuthorization()
    {
        var mediator = CreateMediator();
        mediator.Send(Arg.Any<GetRecommendationPreferenceQuery>(), Arg.Any<CancellationToken>())
            .Returns(ErrorOrFactory.From(new RecommendationPreferenceResult(true)));
        await using var application = CreateApplication(mediator);
        var endpoint = FindEndpoint(application, "/catalog/me/recommendations/preference", "GET");

        var context = await InvokeAsync(application, endpoint, "GET");

        context.Response.StatusCode.Should().Be((int)HttpStatusCode.OK);
        context.Response.Headers.CacheControl.ToString().Should().Be("no-store");
        endpoint.Metadata.GetOrderedMetadata<Microsoft.AspNetCore.Authorization.IAuthorizeData>()
            .Should().NotBeEmpty();
        using var document = await JsonDocument.ParseAsync(context.Response.Body);
        document.RootElement.GetProperty("enabled").GetBoolean().Should().BeTrue();
    }

    [Fact]
    public async Task SetPreference_ReturnsNoContentNoStoreAndRequiresAuthorization()
    {
        var mediator = CreateMediator();
        mediator.Send(Arg.Any<SetRecommendationPreferenceCommand>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success);
        await using var application = CreateApplication(mediator);
        var endpoint = FindEndpoint(application, "/catalog/me/recommendations/preference", "PUT");

        var context = await InvokeAsync(application, endpoint, "PUT", "{\"enabled\":false}");

        context.Response.StatusCode.Should().Be((int)HttpStatusCode.NoContent);
        context.Response.Headers.CacheControl.ToString().Should().Be("no-store");
        endpoint.Metadata.GetOrderedMetadata<Microsoft.AspNetCore.Authorization.IAuthorizeData>()
            .Should().NotBeEmpty();
    }

    private static IMediator CreateMediator() => Substitute.For<IMediator>();

    private static WebApplication CreateApplication(IMediator mediator)
    {
        var builder = WebApplication.CreateBuilder();
        builder.Services.AddSingleton(mediator);
        var application = builder.Build();
        application.UseRouting();
        application.UseMemberAccountController();
        return application;
    }

    private static RouteEndpoint FindEndpoint(WebApplication application, string route, string method) =>
        application.Services.GetRequiredService<EndpointDataSource>()
            .Endpoints.OfType<RouteEndpoint>()
            .Single(endpoint =>
                endpoint.RoutePattern.RawText == route &&
                endpoint.Metadata.GetMetadata<HttpMethodMetadata>()?.HttpMethods.Contains(method) == true);

    private static async Task<DefaultHttpContext> InvokeAsync(
        WebApplication application,
        RouteEndpoint endpoint,
        string method,
        string? body = null)
    {
        var identity = new ClaimsIdentity(
        [
            new Claim("oid", "00000000-0000-0000-0000-000000000001"),
            new Claim("email", "member@example.test"),
            new Claim("given_name", "Camille"),
        ],
        "test");
        var context = new DefaultHttpContext
        {
            RequestServices = application.Services,
            User = new ClaimsPrincipal(identity)
        };
        context.Request.Method = method;
        context.Request.RouteValues["isbn13"] = Isbn13;
        context.Response.Body = new MemoryStream();
        if (body is not null)
        {
            var requestBody = Encoding.UTF8.GetBytes(body);
            context.Request.ContentType = "application/json";
            context.Request.ContentLength = requestBody.Length;
            context.Request.Body = new MemoryStream(requestBody);
            context.Features.Set<IHttpRequestBodyDetectionFeature>(new RequestBodyDetectionFeature());
        }

        context.SetEndpoint(endpoint);
        await endpoint.RequestDelegate!(context);
        context.Response.Body.Position = 0;
        return context;
    }

    private sealed class RequestBodyDetectionFeature : IHttpRequestBodyDetectionFeature
    {
        public bool CanHaveBody => true;
    }
}
