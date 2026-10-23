using FluentAssertions;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using Vole_Papillon_Damour.Application.Common.Interfaces.Services;
using Vole_Papillon_Damour.Application.Isbndb;
using Vole_Papillon_Damour.Domain.BookAggregate.ValueObjects;
using Vole_Papillon_Damour.Domain.IsbndbAggregate;

namespace Vole_Papillon_Damour.Application.tests.Isbndb;

public sealed class IsbndbLookupServiceTests
{
    [Fact]
    public async Task LookupThenLookupMany_UsesOneClientRequestForSameIsbn()
    {
        using var provider = CreateCacheProvider();
        var client = Substitute.For<IIsbndbClient>();
        var isbn = ParseIsbn("9782070409228");
        var book = new IsbndbBook { Isbn13 = isbn.Value, Title = "Les Misérables" };
        client.GetBookAsync(isbn, IsbndbUsage.Scan, Arg.Any<CancellationToken>())
            .Returns(new IsbndbLookup.Found(book));
        var service = CreateService(client, provider);

        var lookup = await service.LookupAsync(isbn, IsbndbUsage.Scan, CancellationToken.None);
        var many = await service.LookupManyAsync([isbn], IsbndbUsage.Scan, CancellationToken.None);

        lookup.Should().Be(new IsbndbLookup.Found(book));
        many.Should().ContainKey(isbn).WhoseValue.Should().Be(new IsbndbLookup.Found(book));
        await client.Received(1).GetBookAsync(isbn, IsbndbUsage.Scan, Arg.Any<CancellationToken>());
        await client.DidNotReceive().GetBooksAsync(
            Arg.Any<IReadOnlyList<Isbn13>>(),
            Arg.Any<IsbndbUsage>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task LookupMany_QueriesOnlyUncachedIsbnsAndConfirmsBatchMissing()
    {
        using var provider = CreateCacheProvider();
        var client = Substitute.For<IIsbndbClient>();
        var cachedIsbn = ParseIsbn("9782070409228");
        var foundIsbn = ParseIsbn("9782070612758");
        var missingIsbn = ParseIsbn("9782253004226");
        var cachedBook = new IsbndbBook { Isbn13 = cachedIsbn.Value, Title = "Cached" };
        var foundBook = new IsbndbBook { Isbn13 = foundIsbn.Value, Title = "Found in bulk" };
        client.GetBookAsync(cachedIsbn, IsbndbUsage.Scan, Arg.Any<CancellationToken>())
            .Returns(new IsbndbLookup.Found(cachedBook));
        client.GetBookAsync(missingIsbn, IsbndbUsage.Scan, Arg.Any<CancellationToken>())
            .Returns(new IsbndbLookup.NotFound());
        client.GetBooksAsync(Arg.Any<IReadOnlyList<Isbn13>>(), IsbndbUsage.Scan, Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                var requested = call.ArgAt<IReadOnlyList<Isbn13>>(0);
                requested.Should().Equal(foundIsbn, missingIsbn);
                return new IsbndbBulkLookup(
                    new Dictionary<Isbn13, IsbndbBook> { [foundIsbn] = foundBook },
                    [missingIsbn]);
            });
        var service = CreateService(client, provider);
        await service.LookupAsync(cachedIsbn, IsbndbUsage.Scan, CancellationToken.None);

        var result = await service.LookupManyAsync(
            [cachedIsbn, foundIsbn, missingIsbn],
            IsbndbUsage.Scan,
            CancellationToken.None);

        result[cachedIsbn].Should().Be(new IsbndbLookup.Found(cachedBook));
        result[foundIsbn].Should().Be(new IsbndbLookup.Found(foundBook));
        result[missingIsbn].Should().Be(new IsbndbLookup.NotFound());
        await client.Received(1).GetBooksAsync(
            Arg.Is<IReadOnlyList<Isbn13>>(values => values.SequenceEqual(new[] { foundIsbn, missingIsbn })),
            IsbndbUsage.Scan,
            Arg.Any<CancellationToken>());

        (await service.LookupAsync(foundIsbn, IsbndbUsage.Scan, CancellationToken.None))
            .Should().Be(new IsbndbLookup.Found(foundBook));
        await client.DidNotReceive().GetBookAsync(foundIsbn, IsbndbUsage.Scan, Arg.Any<CancellationToken>());

        (await service.LookupAsync(missingIsbn, IsbndbUsage.Scan, CancellationToken.None))
            .Should().Be(new IsbndbLookup.NotFound());
        await client.Received(1).GetBookAsync(missingIsbn, IsbndbUsage.Scan, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task LookupAsync_DoesNotCacheQuotaExhaustion()
    {
        using var provider = CreateCacheProvider();
        var client = Substitute.For<IIsbndbClient>();
        var isbn = ParseIsbn("9782070409228");
        var callCount = 0;
        client.GetBookAsync(isbn, IsbndbUsage.Scan, Arg.Any<CancellationToken>())
            .Returns(_ => ++callCount == 1
                ? new IsbndbLookup.QuotaExhausted(DateTime.UtcNow.AddHours(1))
                : new IsbndbLookup.Found(new IsbndbBook { Isbn13 = isbn.Value }));
        var service = CreateService(client, provider);

        (await service.LookupAsync(isbn, IsbndbUsage.Scan, CancellationToken.None))
            .Should().BeOfType<IsbndbLookup.QuotaExhausted>();
        (await service.LookupAsync(isbn, IsbndbUsage.Scan, CancellationToken.None))
            .Should().BeOfType<IsbndbLookup.Found>();

        await client.Received(2).GetBookAsync(isbn, IsbndbUsage.Scan, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task LookupAsync_DoesNotCacheUnavailableResults()
    {
        using var provider = CreateCacheProvider();
        var client = Substitute.For<IIsbndbClient>();
        var isbn = ParseIsbn("9782070409228");
        var callCount = 0;
        client.GetBookAsync(isbn, IsbndbUsage.Scan, Arg.Any<CancellationToken>())
            .Returns(_ => ++callCount == 1
                ? new IsbndbLookup.Unavailable("timeout")
                : new IsbndbLookup.NotFound());
        var service = CreateService(client, provider);

        (await service.LookupAsync(isbn, IsbndbUsage.Scan, CancellationToken.None))
            .Should().Be(new IsbndbLookup.Unavailable("timeout"));
        (await service.LookupAsync(isbn, IsbndbUsage.Scan, CancellationToken.None))
            .Should().Be(new IsbndbLookup.NotFound());

        await client.Received(2).GetBookAsync(isbn, IsbndbUsage.Scan, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task LookupMany_WaitsForAnInFlightSingleLookupOfTheSameIsbn()
    {
        using var provider = CreateCacheProvider();
        var client = Substitute.For<IIsbndbClient>();
        var isbn = ParseIsbn("9782070409228");
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var response = new TaskCompletionSource<IsbndbLookup>(TaskCreationOptions.RunContinuationsAsynchronously);
        var book = new IsbndbBook { Isbn13 = isbn.Value, Title = "Les Misérables" };
        client.GetBookAsync(isbn, IsbndbUsage.Scan, Arg.Any<CancellationToken>())
            .Returns(_ =>
            {
                started.TrySetResult();
                return response.Task;
            });
        var service = CreateService(client, provider);

        var single = service.LookupAsync(isbn, IsbndbUsage.Scan, CancellationToken.None);
        await started.Task.WaitAsync(TimeSpan.FromSeconds(5));
        var many = service.LookupManyAsync([isbn], IsbndbUsage.Scan, CancellationToken.None);
        response.SetResult(new IsbndbLookup.Found(book));

        var singleResult = await single;
        var manyResult = await many;

        singleResult.Should().Be(new IsbndbLookup.Found(book));
        manyResult[isbn].Should().Be(new IsbndbLookup.Found(book));
        await client.Received(1).GetBookAsync(isbn, IsbndbUsage.Scan, Arg.Any<CancellationToken>());
        await client.DidNotReceive().GetBooksAsync(
            Arg.Any<IReadOnlyList<Isbn13>>(),
            Arg.Any<IsbndbUsage>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task LookupAsync_PropagatesCancellation()
    {
        using var provider = CreateCacheProvider();
        var client = Substitute.For<IIsbndbClient>();
        var isbn = ParseIsbn("9782070409228");
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var service = CreateService(client, provider);

        var act = () => service.LookupAsync(isbn, IsbndbUsage.Scan, cancellation.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
        await client.DidNotReceive().GetBookAsync(
            Arg.Any<Isbn13>(),
            Arg.Any<IsbndbUsage>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task LookupAsync_DoesNotSendDefaultInvalidIsbn()
    {
        using var provider = CreateCacheProvider();
        var client = Substitute.For<IIsbndbClient>();
        var service = CreateService(client, provider);

        var result = await service.LookupAsync(default, IsbndbUsage.Scan, CancellationToken.None);

        result.Should().BeOfType<IsbndbLookup.Unavailable>();
        await client.DidNotReceive().GetBookAsync(
            Arg.Any<Isbn13>(),
            Arg.Any<IsbndbUsage>(),
            Arg.Any<CancellationToken>());
    }

    private static ServiceProvider CreateCacheProvider()
    {
        var services = new ServiceCollection();
        services.AddHybridCache();
        return services.BuildServiceProvider();
    }

    private static IIsbndbLookupService CreateService(IIsbndbClient client, ServiceProvider provider) =>
        new IsbndbLookupService(client, provider.GetRequiredService<HybridCache>(), TimeSpan.FromHours(1));

    private static Isbn13 ParseIsbn(string value)
    {
        Isbn13.TryCreate(value, out var isbn13).Should().BeTrue();
        return isbn13;
    }
}
