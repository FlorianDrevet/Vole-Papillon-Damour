using Microsoft.Extensions.Caching.Hybrid;
using Vole_Papillon_Damour.Application.Books.Common;
using Vole_Papillon_Damour.Application.Common.Interfaces.Services;
using Vole_Papillon_Damour.Domain.BookAggregate.ValueObjects;

namespace Vole_Papillon_Damour.Infrastructure.Services.Bibliographic;

/// <summary>
/// Shields BnF, Open Library and Google Books from repeated lookups of the same
/// ISBN within a short window: a burst of scans of the same not-yet-catalogued
/// book (several volunteers, a duplicate copy, a flaky connection retry) only
/// reaches the providers once. This is deliberately short-lived — the durable
/// cache for a book that does resolve is the `Books` table itself, written by
/// the enrichment worker and read directly by <see cref="Vole_Papillon_Damour.Application.Books.Queries.GetBookMetadata.GetBookMetadataQueryHandler"/>
/// before it ever reaches this resolver.
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

    public async Task<BookMetadataResult?> ResolveAsync(Isbn13 isbn13, CancellationToken cancellationToken)
    {
        var entry = await cache.GetOrCreateAsync(
            BuildCacheKey(isbn13),
            (Inner: inner, Isbn13: isbn13),
            static async (state, ct) => new CacheEntry(await state.Inner.ResolveAsync(state.Isbn13, ct)),
            EntryOptions,
            cancellationToken: cancellationToken);

        return entry.Metadata;
    }

    private static string BuildCacheKey(Isbn13 isbn13) => $"book-metadata:{isbn13.Value}";

    // HybridCache requires a non-null value; a "not found" result is wrapped so
    // it can still be cached (avoiding repeated provider calls for a genuinely
    // absent ISBN), while a thrown exception from the inner resolver is never
    // cached — a provider outage must not be remembered as a negative result.
    private sealed record CacheEntry(BookMetadataResult? Metadata);
}
