using Microsoft.Extensions.Caching.Hybrid;
using Vole_Papillon_Damour.Application.Books.Common;
using Vole_Papillon_Damour.Application.Common.Interfaces.Services;

namespace Vole_Papillon_Damour.Infrastructure.Services.Bibliographic;

/// <summary>
/// Caches external reference searches (BnF + Open Library) so that paging back
/// and forth, a duplicate tab, or the same title typed by two volunteers within
/// a few minutes does not each cost two outbound calls. A search failure
/// (<see cref="HttpRequestException"/>) is never cached, so the next attempt
/// still retries the providers.
/// </summary>
public sealed class CachedBibliographicSearchService(
    IBibliographicSearchService inner,
    HybridCache cache) : IBibliographicSearchService
{
    private static readonly HybridCacheEntryOptions EntryOptions = new()
    {
        Expiration = TimeSpan.FromMinutes(15),
        LocalCacheExpiration = TimeSpan.FromMinutes(15),
    };

    public async Task<IReadOnlyList<BookReferenceSearchItem>> SearchAsync(
        string query,
        int page,
        int pageSize,
        CancellationToken cancellationToken)
    {
        var entry = await cache.GetOrCreateAsync(
            BuildCacheKey(query, page, pageSize),
            (Inner: inner, Query: query, Page: page, PageSize: pageSize),
            static async (state, ct) => new CacheEntry(
                await state.Inner.SearchAsync(state.Query, state.Page, state.PageSize, ct)),
            EntryOptions,
            cancellationToken: cancellationToken);

        return entry.Items;
    }

    private static string BuildCacheKey(string query, int page, int pageSize) =>
        $"book-reference-search:{query.Trim().ToLowerInvariant()}:{page}:{pageSize}";

    private sealed record CacheEntry(IReadOnlyList<BookReferenceSearchItem> Items);
}
