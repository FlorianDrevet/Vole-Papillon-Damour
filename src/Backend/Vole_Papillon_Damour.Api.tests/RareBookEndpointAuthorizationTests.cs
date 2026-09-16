using FluentAssertions;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using Vole_Papillon_Damour.Api.Controllers;

namespace Vole_Papillon_Damour.Api.tests;

public sealed class RareBookEndpointAuthorizationTests
{
    [Fact]
    public void Rare_book_controller_exposes_the_planned_registration_extension()
    {
        typeof(RareBookController)
            .GetMethod(nameof(RareBookController.UseRareBookController))
            .Should().NotBeNull();
    }

    [Fact]
    public void Rare_book_endpoints_are_registered_and_protected_by_the_interim_policies()
    {
        var builder = WebApplication.CreateBuilder();
        builder.Services.AddAuthorization();
        builder.Services.AddSingleton(Substitute.For<IMediator>());
        var application = builder.Build();
        application.UseRouting();
        application.UseRareBookController();

        var endpoints = application.Services
            .GetRequiredService<EndpointDataSource>()
            .Endpoints
            .OfType<RouteEndpoint>()
            .ToList();

        endpoints.Any(endpoint =>
            endpoint.RoutePattern.RawText == "/catalog/rare-books" &&
            endpoint.Metadata.GetMetadata<HttpMethodMetadata>() is { HttpMethods: var methods } &&
            methods.Contains("GET"))
            .Should().BeTrue();

        endpoints.Where(endpoint => endpoint.RoutePattern.RawText?.StartsWith("/rare-books/admin", StringComparison.Ordinal) == true)
            .Should().NotBeEmpty()
            .And.OnlyContain(endpoint => endpoint.Metadata
                .GetOrderedMetadata<Microsoft.AspNetCore.Authorization.IAuthorizeData>()
                .Any(data => data.Policy == "Administration"));

        endpoints.Single(endpoint => endpoint.RoutePattern.RawText == "/rare-books/cash/search")
            .Metadata.GetOrderedMetadata<Microsoft.AspNetCore.Authorization.IAuthorizeData>()
            .Should().Contain(data => data.Policy == "ScanVolunteer");
    }

    [Fact]
    public void Every_planned_rare_book_endpoint_is_registered()
    {
        var expected = new (string Route, string Method)[]
        {
            ("/catalog/rare-books", "GET"),
            ("/catalog/rare-books/{slug}", "GET"),
            ("/rare-books/admin", "GET"),
            ("/rare-books/admin/{id:guid}", "GET"),
            ("/rare-books/cash/search", "GET"),
            ("/rare-books/admin", "POST"),
            ("/rare-books/admin/{id:guid}", "PUT"),
            ("/rare-books/admin/{id:guid}/publish", "POST"),
            ("/rare-books/admin/{id:guid}/unpublish", "POST"),
            ("/rare-books/admin/{id:guid}/sold", "POST"),
            ("/rare-books/admin/{id:guid}", "DELETE"),
            ("/rare-books/admin/{id:guid}/photos", "POST"),
            ("/rare-books/admin/{id:guid}/photos/order", "PUT"),
            ("/rare-books/admin/photos/{photoId:guid}", "PATCH"),
            ("/rare-books/admin/photos/{photoId:guid}", "DELETE")
        };

        var actual = RegisteredEndpoints()
            .SelectMany(endpoint => HttpMethods(endpoint).Select(method =>
                (Route: RouteOf(endpoint), Method: method)))
            .ToArray();

        actual.Should().Contain(expected);
    }

    [Fact]
    public void Public_rare_book_reads_are_anonymous()
    {
        RegisteredEndpoints()
            .Where(endpoint => RouteOf(endpoint).StartsWith("/catalog/rare-books", StringComparison.Ordinal))
            .Should()
            .HaveCount(2)
            .And.OnlyContain(endpoint => !endpoint.Metadata
                .GetOrderedMetadata<Microsoft.AspNetCore.Authorization.IAuthorizeData>()
                .Any());
    }

    private static IReadOnlyList<RouteEndpoint> RegisteredEndpoints()
    {
        var builder = WebApplication.CreateBuilder();
        builder.Services.AddAuthorization();
        builder.Services.AddSingleton(Substitute.For<IMediator>());
        var application = builder.Build();
        application.UseRouting();
        application.UseRareBookController();

        return application.Services
            .GetRequiredService<EndpointDataSource>()
            .Endpoints
            .OfType<RouteEndpoint>()
            .ToList();
    }

    private static IReadOnlyCollection<string> HttpMethods(Endpoint endpoint) =>
        endpoint.Metadata.GetMetadata<HttpMethodMetadata>()?.HttpMethods ?? [];

    private static string RouteOf(RouteEndpoint endpoint) =>
        endpoint.RoutePattern.RawText ?? string.Empty;
}
