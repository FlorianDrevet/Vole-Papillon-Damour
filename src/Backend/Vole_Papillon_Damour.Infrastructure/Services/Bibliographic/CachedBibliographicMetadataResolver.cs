using Microsoft.Extensions.Caching.Hybrid;
using Vole_Papillon_Damour.Application.Books.Common;
using Vole_Papillon_Damour.Application.Common.Interfaces.Services;
using Vole_Papillon_Damour.Application.Isbndb;
using Vole_Papillon_Damour.Domain.BookAggregate.ValueObjects;
using Vole_Papillon_Damour.Domain.IsbndbAggregate;

namespace Vole_Papillon_Damour.Infrastructure.Services.Bibliographic;

/// <summary>
/// Caches the resolved notice by ISBN. A complete ISBNdb notice also satisfies later
/// requests for bibliographic details; a less complete fallback entry is upgraded
/// when a details refresh is requested.
/// </summary>
public sealed class CachedBibliographicMetadataResolver(
    IBibliographicMetadataResolver inner,
    HybridCache cache) : IBibliographicMetadataResolver
{
    private static readonly HybridCacheEntryOptions EntryOptions = new()
    {
        Expiration = TimeSpan.FromHours(6),
        LocalCacheExpiration = TimeSpan.FromHours(6),
    };

    public async Task<BookMetadataResult?> ResolveAsync(
        Isbn13 isbn13,
        CancellationToken cancellationToken,
        bool includeBibliographicDetails = false,
        IsbndbUsage usage = IsbndbUsage.Scan,
        IsbndbLookup? prefetchedIsbndbLookup = null)
    {
        var key = BuildCacheKey(isbn13);
        if (prefetchedIsbndbLookup is not null)
        {
            await cache.RemoveAsync(key, cancellationToken);
        }

        var state = new ResolveState(
            inner,
            isbn13,
            includeBibliographicDetails,
            usage,
            prefetchedIsbndbLookup);
        var entry = await GetOrCreateAsync(key, state, cancellationToken);
        if (includeBibliographicDetails && !entry.IncludesBibliographicDetails)
        {
            await cache.RemoveAsync(key, cancellationToken);
            entry = await GetOrCreateAsync(key, state with { PrefetchedIsbndbLookup = null }, cancellationToken);
        }

        return entry.Metadata;
    }

    public Task<string?> ResolveWorkIdAsync(Isbn13 isbn13, CancellationToken cancellationToken) =>
        inner.ResolveWorkIdAsync(isbn13, cancellationToken);

    private ValueTask<CacheEntry> GetOrCreateAsync(
        string key,
        ResolveState state,
        CancellationToken cancellationToken) =>
        cache.GetOrCreateAsync(
            key,
            state,
            static async (current, ct) =>
            {
                var metadata = await current.Inner.ResolveAsync(
                    current.Isbn13,
                    ct,
                    current.IncludeBibliographicDetails,
                    current.Usage,
                    current.PrefetchedIsbndbLookup);
                var includesDetails = current.IncludeBibliographicDetails ||
                                      metadata?.IsbndbOutcome == IsbndbOutcome.Found;
                return new CacheEntry(metadata, includesDetails);
            },
            EntryOptions,
            cancellationToken: cancellationToken);

    private static string BuildCacheKey(Isbn13 isbn13) => $"book-metadata:{isbn13.Value}";

    private sealed record ResolveState(
        IBibliographicMetadataResolver Inner,
        Isbn13 Isbn13,
        bool IncludeBibliographicDetails,
        IsbndbUsage Usage,
        IsbndbLookup? PrefetchedIsbndbLookup);

    private sealed record CacheEntry(BookMetadataResult? Metadata, bool IncludesBibliographicDetails);
}
