using FluentAssertions;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using Vole_Papillon_Damour.Application.Books.Common;
using Vole_Papillon_Damour.Application.Common.Interfaces.Services;
using Vole_Papillon_Damour.Domain.BookAggregate.ValueObjects;
using Vole_Papillon_Damour.Infrastructure.Services.Bibliographic;

namespace Vole_Papillon_Damour.Infrastructure.tests.Bibliographic;

public sealed class CachedBibliographicMetadataResolverTests
{
    [Fact]
    public async Task ResolveAsync_WhenCalledTwiceForTheSameIsbn_OnlyCallsTheInnerResolverOnce()
    {
        Isbn13.TryCreate("9782070363735", out var isbn13).Should().BeTrue();
        var metadata = new BookMetadataResult(
            isbn13.Value, "Le Petit Prince", null, null, null, null, "BnF", null,
            new DateTimeOffset(2026, 9, 3, 8, 0, 0, TimeSpan.Zero));
        var inner = Substitute.For<IBibliographicMetadataResolver>();
        inner.ResolveAsync(isbn13, Arg.Any<CancellationToken>()).Returns(metadata);
        var resolver = new CachedBibliographicMetadataResolver(inner, CreateCache());

        var first = await resolver.ResolveAsync(isbn13, CancellationToken.None);
        var second = await resolver.ResolveAsync(isbn13, CancellationToken.None);

        first.Should().Be(metadata);
        second.Should().Be(metadata);
        await inner.Received(1).ResolveAsync(isbn13, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ResolveAsync_WhenNotFound_CachesTheNegativeResultToo()
    {
        Isbn13.TryCreate("9782070363735", out var isbn13).Should().BeTrue();
        var inner = Substitute.For<IBibliographicMetadataResolver>();
        inner.ResolveAsync(isbn13, Arg.Any<CancellationToken>()).Returns((BookMetadataResult?)null);
        var resolver = new CachedBibliographicMetadataResolver(inner, CreateCache());

        var first = await resolver.ResolveAsync(isbn13, CancellationToken.None);
        var second = await resolver.ResolveAsync(isbn13, CancellationToken.None);

        first.Should().BeNull();
        second.Should().BeNull();
        await inner.Received(1).ResolveAsync(isbn13, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ResolveAsync_WhenTheInnerResolverFails_DoesNotCacheTheFailure()
    {
        Isbn13.TryCreate("9782070363735", out var isbn13).Should().BeTrue();
        var inner = Substitute.For<IBibliographicMetadataResolver>();
        inner.ResolveAsync(isbn13, Arg.Any<CancellationToken>())
            .Returns<BookMetadataResult?>(_ => throw new HttpRequestException());
        var resolver = new CachedBibliographicMetadataResolver(inner, CreateCache());

        var act = () => resolver.ResolveAsync(isbn13, CancellationToken.None);

        await act.Should().ThrowAsync<HttpRequestException>();
        await act.Should().ThrowAsync<HttpRequestException>();
        await inner.Received(2).ResolveAsync(isbn13, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ResolveAsync_ForDifferentIsbns_CallsTheInnerResolverForEachOne()
    {
        Isbn13.TryCreate("9782070363735", out var firstIsbn).Should().BeTrue();
        Isbn13.TryCreate("9782070368228", out var secondIsbn).Should().BeTrue();
        var inner = Substitute.For<IBibliographicMetadataResolver>();
        inner.ResolveAsync(Arg.Any<Isbn13>(), Arg.Any<CancellationToken>())
            .Returns((BookMetadataResult?)null);
        var resolver = new CachedBibliographicMetadataResolver(inner, CreateCache());

        await resolver.ResolveAsync(firstIsbn, CancellationToken.None);
        await resolver.ResolveAsync(secondIsbn, CancellationToken.None);

        await inner.Received(1).ResolveAsync(firstIsbn, Arg.Any<CancellationToken>());
        await inner.Received(1).ResolveAsync(secondIsbn, Arg.Any<CancellationToken>());
    }

    private static HybridCache CreateCache()
    {
        var services = new ServiceCollection();
        services.AddHybridCache();
        return services.BuildServiceProvider().GetRequiredService<HybridCache>();
    }
}
