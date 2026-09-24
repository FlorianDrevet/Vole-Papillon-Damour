using System.Net;
using System.Text.Json;
using ErrorOr;
using FluentAssertions;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using Vole_Papillon_Damour.Api.Common.RateLimiting;
using Vole_Papillon_Damour.Api.Controllers;
using Vole_Papillon_Damour.Application.Books.Common;
using Vole_Papillon_Damour.Application.Recommendations.Queries.GetSimilarBooks;
using Vole_Papillon_Damour.Domain.RecommendationAggregate.ValueObjects;

namespace Vole_Papillon_Damour.Api.tests.Recommendations;

public sealed class SimilarBooksEndpointTests
{
    private const string Isbn13 = "9782070408504";

    [Fact]
    public async Task GetSimilarBooks_ReturnsFlattenedBookResponseWithReasonAndPublicCacheControl()
    {
        var mediator = Substitute.For<IMediator>();
        IReadOnlyList<SimilarBookResult> items =
        [
            new SimilarBookResult(
                new PublicCatalogBookResult(
                    Isbn13,
                    "Tome suivant",
                    "Auteur",
                    "Éditeur",
                    2025,
                    "Poche",
                    "fr",
                    "Roman",
                    null,
                    null,
                    2,
                    0,
                    null,
                    null,
                    DateTimeOffset.Parse("2026-09-01T00:00:00Z"),
                    DateTimeOffset.Parse("2026-09-02T00:00:00Z"),
                    false),
                NeighborReason.NextTome)
        ];
        var response = ErrorOrFactory.From<IReadOnlyList<SimilarBookResult>>(items);
        mediator.Send(Arg.Any<GetSimilarBooksQuery>(), Arg.Any<CancellationToken>()).Returns(response);
        await using var application = CreateApplication(mediator);
        var endpoint = FindSimilarEndpoint(application);

        var context = await InvokeAsync(application, endpoint);

        context.Response.StatusCode.Should().Be((int)HttpStatusCode.OK);
        context.Response.Headers.CacheControl.ToString().Should().Be("public, max-age=300");
        using var document = await JsonDocument.ParseAsync(context.Response.Body);
        var book = document.RootElement.GetProperty("books")[0];
        book.GetProperty("isbn13").GetString().Should().Be(Isbn13);
        book.GetProperty("title").GetString().Should().Be("Tome suivant");
        book.GetProperty("reason").GetString().Should().Be("NextTome");
        book.TryGetProperty("book", out _).Should().BeFalse();

        endpoint.Metadata.GetMetadata<EnableRateLimitingAttribute>()?.PolicyName
            .Should().Be(RateLimitingPolicies.PublicCatalog);
        endpoint.Metadata.GetOrderedMetadata<Microsoft.AspNetCore.Authorization.IAllowAnonymous>()
            .Should().NotBeEmpty();
    }

    private static WebApplication CreateApplication(IMediator mediator)
    {
        var builder = WebApplication.CreateBuilder();
        builder.Services.AddSingleton(mediator);
        var application = builder.Build();
        application.UseRouting();
        application.UseBookController();
        return application;
    }

    private static RouteEndpoint FindSimilarEndpoint(WebApplication application) =>
        application.Services.GetRequiredService<EndpointDataSource>()
            .Endpoints.OfType<RouteEndpoint>()
            .Single(endpoint =>
                endpoint.RoutePattern.RawText == "/catalog/books/{isbn13}/similar" &&
                endpoint.Metadata.GetMetadata<HttpMethodMetadata>()?.HttpMethods.Contains("GET") == true);

    private static async Task<DefaultHttpContext> InvokeAsync(
        WebApplication application,
        RouteEndpoint endpoint)
    {
        var context = new DefaultHttpContext
        {
            RequestServices = application.Services,
            User = new System.Security.Claims.ClaimsPrincipal()
        };
        context.Request.Method = "GET";
        context.Request.RouteValues["isbn13"] = Isbn13;
        context.Response.Body = new MemoryStream();
        context.SetEndpoint(endpoint);
        await endpoint.RequestDelegate!(context);
        context.Response.Body.Position = 0;
        return context;
    }
}
