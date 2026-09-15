using FluentAssertions;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using Vole_Papillon_Damour.Application.Books.Common;
using Vole_Papillon_Damour.Application.Common.Interfaces.Services;
using Vole_Papillon_Damour.Infrastructure.Services.Bibliographic;

namespace Vole_Papillon_Damour.Infrastructure.tests.Bibliographic;

public sealed class CachedBibliographicSearchServiceTests
{
    [Fact]
    public async Task SearchAsync_WhenCalledTwiceWithTheSameArguments_OnlyCallsTheInnerServiceOnce()
    {
        var items = new[]
        {
            new BookReferenceSearchItem("9782070363735", "OL123W", "Le Petit Prince", null, null, null, null, "BnF"),
        };
        var inner = Substitute.For<IBibliographicSearchService>();
        inner.SearchAsync("petit prince", 1, 20, Arg.Any<CancellationToken>())
            .Returns(items);
        var service = new CachedBibliographicSearchService(inner, CreateCache());

        var first = await service.SearchAsync("petit prince", 1, 20, CancellationToken.None);
        var second = await service.SearchAsync("  Petit Prince  ", 1, 20, CancellationToken.None);

        first.Should().BeEquivalentTo(items);
        second.Should().BeEquivalentTo(items);
        await inner.Received(1).SearchAsync(Arg.Any<string>(), 1, 20, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SearchAsync_WhenTheInnerServiceFails_DoesNotCacheTheFailure()
    {
        var inner = Substitute.For<IBibliographicSearchService>();
        inner.SearchAsync("petit prince", 1, 20, Arg.Any<CancellationToken>())
            .Returns<IReadOnlyList<BookReferenceSearchItem>>(_ => throw new HttpRequestException());
        var service = new CachedBibliographicSearchService(inner, CreateCache());

        var act = () => service.SearchAsync("petit prince", 1, 20, CancellationToken.None);

        await act.Should().ThrowAsync<HttpRequestException>();
        await act.Should().ThrowAsync<HttpRequestException>();
        await inner.Received(2).SearchAsync("petit prince", 1, 20, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SearchAsync_ForDifferentPages_CallsTheInnerServiceForEachPage()
    {
        var inner = Substitute.For<IBibliographicSearchService>();
        inner.SearchAsync(Arg.Any<string>(), Arg.Any<int>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(Array.Empty<BookReferenceSearchItem>());
        var service = new CachedBibliographicSearchService(inner, CreateCache());

        await service.SearchAsync("petit prince", 1, 20, CancellationToken.None);
        await service.SearchAsync("petit prince", 2, 20, CancellationToken.None);

        await inner.Received(1).SearchAsync("petit prince", 1, 20, Arg.Any<CancellationToken>());
        await inner.Received(1).SearchAsync("petit prince", 2, 20, Arg.Any<CancellationToken>());
    }

    private static HybridCache CreateCache()
    {
        var services = new ServiceCollection();
        services.AddHybridCache();
        return services.BuildServiceProvider().GetRequiredService<HybridCache>();
    }
}
