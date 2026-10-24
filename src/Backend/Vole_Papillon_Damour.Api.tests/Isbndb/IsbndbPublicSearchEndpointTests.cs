using FluentAssertions;
using ErrorOr;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using Vole_Papillon_Damour.Api.Common.RateLimiting;
using Vole_Papillon_Damour.Api.Controllers;
using Vole_Papillon_Damour.Application.Isbndb.Queries.SearchIsbndbEditions;

namespace Vole_Papillon_Damour.Api.tests.Isbndb;

public sealed class IsbndbPublicSearchEndpointTests
{
    [Fact]
    public async Task SearchEndpoint_AllowsAnonymousRequestsAndUsesTheDedicatedLimiter()
    {
        var builder = WebApplication.CreateBuilder();
        builder.Services.AddAuthorization();
        builder.Services.AddSingleton(Substitute.For<IMediator>());
        await using var application = builder.Build();
        application.UseRouting();
        application.UseBibliographicReferenceController();

        var endpoint = application.Services.GetRequiredService<EndpointDataSource>()
            .Endpoints.OfType<RouteEndpoint>()
            .Single(candidate => candidate.RoutePattern.RawText == "/catalog/reference/isbndb");

        endpoint.Metadata.GetMetadata<IAllowAnonymous>().Should().NotBeNull();
        endpoint.Metadata.GetOrderedMetadata<EnableRateLimitingAttribute>()
            .Should().ContainSingle(attribute => attribute.PolicyName == RateLimitingPolicies.IsbndbSearch);
        endpoint.Metadata.GetOrderedMetadata<IAuthorizeData>().Should().BeEmpty();
    }

    [Fact]
    public async Task SearchEndpoint_Returns429AfterConfiguredVisitorLimit()
    {
        var mediator = Substitute.For<IMediator>();
        mediator.Send(Arg.Any<SearchIsbndbEditionsQuery>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<ErrorOr<SearchIsbndbEditionsResult>>(
                new SearchIsbndbEditionsResult([], 0, false, "unavailable")));
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        builder.Services.AddAuthorization();
        builder.Services.AddSingleton(mediator);
        builder.Services.AddRateLimiting(isbndbSearchPermitLimit: 1);
        await using var application = builder.Build();
        application.UseRouting();
        application.UseRateLimiter();
        application.UseBibliographicReferenceController();
        await application.StartAsync();

        var address = application.Services.GetRequiredService<IServer>()
            .Features.Get<IServerAddressesFeature>()!.Addresses.Single();
        using var client = new HttpClient { BaseAddress = new Uri(address) };
        var first = await client.GetAsync("/catalog/reference/isbndb?q=dune");
        var second = await client.GetAsync("/catalog/reference/isbndb?q=foundation");

        first.StatusCode.Should().Be(System.Net.HttpStatusCode.OK);
        second.StatusCode.Should().Be(System.Net.HttpStatusCode.TooManyRequests);
        await mediator.Received(1).Send(
            Arg.Any<SearchIsbndbEditionsQuery>(),
            Arg.Any<CancellationToken>());
    }
}
