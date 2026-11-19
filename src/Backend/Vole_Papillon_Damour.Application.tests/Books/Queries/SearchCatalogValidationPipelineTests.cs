using ErrorOr;
using FluentAssertions;
using MediatR;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using Vole_Papillon_Damour.Application.Books.Common;
using Vole_Papillon_Damour.Application.Books.Queries.SearchCatalog;
using Vole_Papillon_Damour.Application.Common.Interfaces.Persistence;
using Vole_Papillon_Damour.Application.Common.Interfaces.Services;

namespace Vole_Papillon_Damour.Application.tests.Books.Queries;

public sealed class SearchCatalogValidationPipelineTests
{
    [Fact]
    public async Task Send_WhenSearchTextIsTooLong_ReturnsValidationErrorFromThePipeline()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddApplication();
        services.AddSingleton(Substitute.For<IProjectDbContext>());
        services.AddSingleton(Substitute.For<IDateTimeProvider>());
        services.AddSingleton(Substitute.For<HybridCache>());
        await using var provider = services.BuildServiceProvider();
        var sender = provider.GetRequiredService<ISender>();

        var result = await sender.Send(new SearchCatalogQuery(
            new string('a', 101), null, PublicCatalogAvailabilityFilter.All,
            RareOnly: false, PublicCatalogSortOrder.Relevance, Page: 1, PageSize: 24));

        result.IsError.Should().BeTrue();
        result.FirstError.Type.Should().Be(ErrorType.Validation);
        result.FirstError.Code.Should().Be("Search");
    }
}
