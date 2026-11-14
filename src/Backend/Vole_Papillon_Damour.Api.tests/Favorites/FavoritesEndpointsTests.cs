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
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using Vole_Papillon_Damour.Api.Common.RateLimiting;
using Vole_Papillon_Damour.Api.Controllers;
using Vole_Papillon_Damour.Application.Common.Interfaces.Services;
using Vole_Papillon_Damour.Application.Favorites.Commands.AddMyFavorite;
using Vole_Papillon_Damour.Application.Favorites.Commands.DeclareAnonymousFavorite;
using Vole_Papillon_Damour.Application.Favorites.Commands.MergeMyFavorites;
using Vole_Papillon_Damour.Application.Favorites.Commands.RemoveMyFavorite;
using Vole_Papillon_Damour.Application.Favorites.Commands.WithdrawAnonymousFavorite;
using Vole_Papillon_Damour.Application.Favorites.Common;
using Vole_Papillon_Damour.Application.Favorites.Queries.GetMyFavorites;
using Vole_Papillon_Damour.Application.Recommendations.Commands.SetRecommendationPreference;
using Vole_Papillon_Damour.Application.Recommendations.Queries.GetMyRecommendations;
using Vole_Papillon_Damour.Application.Recommendations.Queries.GetRecommendationPreference;
using Vole_Papillon_Damour.Application.Recommendations.Queries.GetSuggestionsForFavorites;
using Vole_Papillon_Damour.Domain.BookAggregate.ValueObjects;
using Vole_Papillon_Damour.Domain.RecommendationAggregate.ValueObjects;

namespace Vole_Papillon_Damour.Api.tests.Favorites;

public sealed class FavoritesEndpointsTests
{
    private const string Isbn = "9782070408504";
    private static readonly Guid ExternalId = Guid.Parse("7f0b0f0e-0000-0000-0000-00000000c0de");
    private static readonly DateTimeOffset Now = DateTimeOffset.Parse("2026-11-14T12:00:00Z");

    [Fact]
    public async Task GetMemberFavorites_RequiresAuthenticationAndReturnsGroupedFavorites()
    {
        var mediator = CreateMediator();
        mediator.Send(Arg.Any<GetMyFavoritesQuery>(), Arg.Any<CancellationToken>())
            .Returns(ErrorOrFactory.From(new MyFavoritesResult(
                Now,
                1,
                200,
                [new MyFavoriteWorkResult(
                    "work:123",
                    "123",
                    "Dune",
                    "Frank Herbert",
                    Now,
                    1,
                    false,
                    [new MyFavoriteEditionResult(Isbn, "Dune", "Frank Herbert", null, Now, 1, 0, 3, "Ready")])])));
        await using var application = CreateApplication(mediator);
        var endpoint = FindEndpoint(application, "GET", "/catalog/me/favorites");

        var unauthorized = await InvokeAsync(application, endpoint, "GET", principal: new ClaimsPrincipal());
        var response = await InvokeAsync(application, endpoint, "GET", principal: AuthenticatedPrincipal());

        unauthorized.Response.StatusCode.Should().Be((int)HttpStatusCode.Unauthorized);
        response.Response.StatusCode.Should().Be((int)HttpStatusCode.OK);
        response.Response.Headers.CacheControl.ToString().Should().Be("no-store");
        endpoint.Metadata.GetOrderedMetadata<Microsoft.AspNetCore.Authorization.IAuthorizeData>().Should().NotBeEmpty();
        using var json = await JsonDocument.ParseAsync(response.Response.Body);
        json.RootElement.GetProperty("total").GetInt32().Should().Be(1);
        json.RootElement.GetProperty("works")[0].GetProperty("editions")[0]
            .GetProperty("isbn13").GetString().Should().Be(Isbn);
        await mediator.Received(1).Send(Arg.Is<GetMyFavoritesQuery>(query =>
            query.ExternalId == ExternalId && query.Email == "camille@example.test"), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task AddAndRemoveMemberFavoriteForwardTheMemberAndIsbn()
    {
        Isbn13.TryCreate(Isbn, out var isbn13).Should().BeTrue();
        var mediator = CreateMediator();
        mediator.Send(Arg.Any<AddMyFavoriteCommand>(), Arg.Any<CancellationToken>())
            .Returns(ErrorOrFactory.From(new FavoriteAddedResult(isbn13, false, 1)));
        mediator.Send(Arg.Any<RemoveMyFavoriteCommand>(), Arg.Any<CancellationToken>())
            .Returns(Result.Deleted);
        await using var application = CreateApplication(mediator);
        var put = FindEndpoint(application, "PUT", "/catalog/me/favorites/{isbn13}");
        var delete = FindEndpoint(application, "DELETE", "/catalog/me/favorites/{isbn13}");

        var added = await InvokeAsync(application, put, "PUT", principal: AuthenticatedPrincipal(), routeValues: new Dictionary<string, object?> { ["isbn13"] = Isbn });
        var removed = await InvokeAsync(application, delete, "DELETE", principal: AuthenticatedPrincipal(), routeValues: new Dictionary<string, object?> { ["isbn13"] = Isbn });

        added.Response.StatusCode.Should().Be((int)HttpStatusCode.OK);
        removed.Response.StatusCode.Should().Be((int)HttpStatusCode.NoContent);
        await mediator.Received(1).Send(Arg.Is<AddMyFavoriteCommand>(command =>
            command.ExternalId == ExternalId && command.Email == "camille@example.test" && command.Isbn13 == Isbn), Arg.Any<CancellationToken>());
        await mediator.Received(1).Send(Arg.Is<RemoveMyFavoriteCommand>(command =>
            command.ExternalId == ExternalId && command.Email == "camille@example.test" && command.Isbn13 == Isbn), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task MergeMemberFavoritesForwardsEntriesAndIdentity()
    {
        var mediator = CreateMediator();
        mediator.Send(Arg.Any<MergeMyFavoritesCommand>(), Arg.Any<CancellationToken>())
            .Returns(ErrorOrFactory.From(new MergeMyFavoritesResult(1, 0, 1)));
        await using var application = CreateApplication(mediator);
        var endpoint = FindEndpoint(application, "POST", "/catalog/me/favorites/merge");

        var response = await InvokeAsync(
            application,
            endpoint,
            "POST",
            "{\"entries\":[{\"isbn13\":\"9782070408504\",\"addedAt\":\"2026-11-14T11:00:00Z\"}]}",
            AuthenticatedPrincipal());

        response.Response.StatusCode.Should().Be((int)HttpStatusCode.OK);
        await mediator.Received(1).Send(Arg.Is<MergeMyFavoritesCommand>(command =>
            command.ExternalId == ExternalId &&
            command.Entries.Count == 1 &&
            command.Entries[0].Isbn13 == Isbn), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task AnonymousFavoriteDeclarationsHaveCreationPolicyAndNoAuthorization()
    {
        var mediator = CreateMediator();
        mediator.Send(Arg.Any<DeclareAnonymousFavoriteCommand>(), Arg.Any<CancellationToken>())
            .Returns(ErrorOrFactory.From(new AnonymousFavoriteResult(Isbn, 2)));
        mediator.Send(Arg.Any<WithdrawAnonymousFavoriteCommand>(), Arg.Any<CancellationToken>())
            .Returns(ErrorOrFactory.From(new AnonymousFavoriteResult(Isbn, 1)));
        await using var application = CreateApplication(mediator);
        var post = FindEndpoint(application, "POST", "/catalog/favorites/declarations");
        var delete = FindEndpoint(application, "DELETE", "/catalog/favorites/declarations/{isbn13}");

        var declared = await InvokeAsync(application, post, "POST", "{\"isbn13\":\"9782070408504\"}", new ClaimsPrincipal());
        var withdrawn = await InvokeAsync(
            application,
            delete,
            "DELETE",
            principal: new ClaimsPrincipal(),
            routeValues: new Dictionary<string, object?> { ["isbn13"] = Isbn });

        declared.Response.StatusCode.Should().Be((int)HttpStatusCode.OK);
        withdrawn.Response.StatusCode.Should().Be((int)HttpStatusCode.OK);
        post.Metadata.GetOrderedMetadata<Microsoft.AspNetCore.Authorization.IAuthorizeData>().Should().BeEmpty();
        post.Metadata.GetMetadata<EnableRateLimitingAttribute>()?.PolicyName.Should().Be(RateLimitingPolicies.FavoriteReference);
        delete.Metadata.GetOrderedMetadata<Microsoft.AspNetCore.Authorization.IAuthorizeData>().Should().BeEmpty();
    }

    [Fact]
    public async Task AnonymousSuggestionsAreNoStoreAndRejectMoreThanFiftyFavorites()
    {
        var mediator = CreateMediator();
        mediator.Send(Arg.Any<GetSuggestionsForFavoritesQuery>(), Arg.Any<CancellationToken>())
            .Returns(ErrorOrFactory.From(new SuggestionsForFavoritesResult(
                RecommendationStatus.Enabled,
                [],
                [],
                3)));
        await using var application = CreateApplication(mediator);
        var endpoint = FindEndpoint(application, "POST", "/catalog/favorites/suggestions");
        var fiftyOne = string.Join(",", Enumerable.Range(0, 51).Select(index => $"\"{CreateValidIsbn(index)}\""));

        var tooMany = await InvokeAsync(
            application,
            endpoint,
            "POST",
            $"{{\"isbn13s\":[{fiftyOne}],\"minimumHearts\":3}}",
            new ClaimsPrincipal());
        var response = await InvokeAsync(
            application,
            endpoint,
            "POST",
            "{\"isbn13s\":[\"9782070408504\"],\"minimumHearts\":3}",
            new ClaimsPrincipal());

        tooMany.Response.StatusCode.Should().Be((int)HttpStatusCode.BadRequest);
        response.Response.StatusCode.Should().Be((int)HttpStatusCode.OK);
        response.Response.Headers.CacheControl.ToString().Should().Be("no-store");
        endpoint.Metadata.GetOrderedMetadata<Microsoft.AspNetCore.Authorization.IAuthorizeData>().Should().BeEmpty();
        endpoint.Metadata.GetMetadata<EnableRateLimitingAttribute>()?.PolicyName.Should().Be(RateLimitingPolicies.PublicCatalog);
    }

    [Fact]
    public async Task RecommendationPreferenceForwardsMinimumHeartsAndConsentChoice()
    {
        var mediator = CreateMediator();
        mediator.Send(Arg.Any<SetRecommendationPreferenceCommand>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success);
        await using var application = CreateApplication(mediator);
        var endpoint = FindEndpoint(application, "PUT", "/catalog/me/recommendations/preference");

        var response = await InvokeAsync(
            application,
            endpoint,
            "PUT",
            "{\"enabled\":true,\"minimumHearts\":4,\"fairDigestConsent\":true}",
            AuthenticatedPrincipal());

        response.Response.StatusCode.Should().Be((int)HttpStatusCode.NoContent);
        await mediator.Received(1).Send<ErrorOr<Success>>(
            Arg.Is<MediatR.IRequest<ErrorOr<Success>>>(request =>
                ((SetRecommendationPreferenceCommand)request).ExternalId == ExternalId.ToString("D") &&
                ((SetRecommendationPreferenceCommand)request).Enabled &&
                ((SetRecommendationPreferenceCommand)request).MinimumHearts == 4 &&
                ((SetRecommendationPreferenceCommand)request).FairDigestConsent == true),
            Arg.Any<CancellationToken>());
    }

    private static IMediator CreateMediator() => Substitute.For<IMediator>();

    private static WebApplication CreateApplication(IMediator mediator)
    {
        var builder = WebApplication.CreateBuilder();
        builder.Services.AddAuthorization();
        builder.Services.AddSingleton(mediator);
        var settings = Substitute.For<IRecommendationSettings>();
        settings.Enabled.Returns(true);
        builder.Services.AddSingleton(settings);
        var application = builder.Build();
        application.UseRouting();
        application.UseBookController();
        application.UseMemberAccountController();
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
        context.Response.Body = new MemoryStream();
        if (body is not null)
        {
            var bytes = Encoding.UTF8.GetBytes(body);
            context.Request.Body = new MemoryStream(bytes);
            context.Request.ContentLength = bytes.Length;
            context.Features.Set<IHttpRequestBodyDetectionFeature>(new RequestBodyDetectionFeature());
        }

        if (routeValues is not null)
        {
            foreach (var (key, value) in routeValues)
            {
                context.Request.RouteValues[key] = value;
            }
        }

        context.SetEndpoint(endpoint);
        await endpoint.RequestDelegate!(context);
        context.Response.Body.Position = 0;
        return context;
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

    private static string CreateValidIsbn(int index)
    {
        var body = $"97800000{index:D4}";
        var sum = body.Select((digit, position) =>
            (digit - '0') * (position % 2 == 0 ? 1 : 3)).Sum();
        return body + ((10 - (sum % 10)) % 10);
    }

    private sealed class RequestBodyDetectionFeature : IHttpRequestBodyDetectionFeature
    {
        public bool CanHaveBody => true;
    }
}
