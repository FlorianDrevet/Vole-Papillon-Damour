using FluentAssertions;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using Vole_Papillon_Damour.Api.Controllers;

namespace Vole_Papillon_Damour.Api.tests.MemberAccount;

public sealed class GenreFavoriteEndpointRegistrationTests
{
    [Fact]
    public async Task DeleteGenreFavorite_EndpointCanBeRegistered()
    {
        var builder = WebApplication.CreateBuilder();
        builder.Services.AddAuthorization();
        builder.Services.AddSingleton(Substitute.For<IMediator>());
        await using var application = builder.Build();
        application.UseRouting();
        application.UseMemberAccountController();

        var endpoint = application.Services.GetRequiredService<EndpointDataSource>()
            .Endpoints.OfType<RouteEndpoint>()
            .SingleOrDefault(route =>
                route.RoutePattern.RawText == "/catalog/me/genre-favorites" &&
                route.Metadata.GetMetadata<HttpMethodMetadata>()?.HttpMethods.Contains("DELETE") == true);

        endpoint.Should().NotBeNull();
    }
}
