using System.Text.Json;
using System.Xml;
using Microsoft.Extensions.Logging;
using Vole_Papillon_Damour.Application.Books.Common;
using Vole_Papillon_Damour.Application.Common.Interfaces.Services;

namespace Vole_Papillon_Damour.Infrastructure.Services.Bibliographic;

/// <summary>
/// Searches BnF and Open Library in parallel and merges the editions:
/// BnF editions come first and win on shared ISBNs, Open Library only fills their gaps.
/// </summary>
public sealed class BibliographicSearchService(
    IBnfSruSearchClient bnfClient,
    IOpenLibraryClient openLibraryClient,
    ILogger<BibliographicSearchService> logger)
    : IBibliographicSearchService
{
    private const string BnfProvider = "BnF";
    private const string OpenLibraryProvider = "OpenLibrary";

    public async Task<IReadOnlyList<BookReferenceSearchItem>> SearchAsync(
        string query,
        int page,
        int pageSize,
        CancellationToken cancellationToken)
    {
        var bnfSearch = TrySearchAsync(
            BnfProvider,
            () => bnfClient.SearchAsync(query, page, pageSize, cancellationToken),
            cancellationToken);
        var openLibrarySearch = TrySearchAsync(
            OpenLibraryProvider,
            () => openLibraryClient.SearchAsync(query, page, pageSize, cancellationToken),
            cancellationToken);
        var results = await Task.WhenAll(bnfSearch, openLibrarySearch);
        var bnfResult = results[0];
        var openLibraryResult = results[1];

        if (bnfResult.Failed && openLibraryResult.Failed)
        {
            throw new HttpRequestException("All bibliographic search providers failed.");
        }

        return Merge(bnfResult.Items, openLibraryResult.Items);
    }

    private static IReadOnlyList<BookReferenceSearchItem> Merge(
        IReadOnlyList<BookReferenceSearchItem> bnfItems,
        IReadOnlyList<BookReferenceSearchItem> openLibraryItems)
    {
        var openLibraryByIsbn = openLibraryItems
            .Where(item => item.Isbn13 is not null)
            .GroupBy(item => item.Isbn13!, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.Ordinal);
        var seenIsbns = new HashSet<string>(StringComparer.Ordinal);
        var merged = new List<BookReferenceSearchItem>();

        foreach (var bnfItem in bnfItems)
        {
            if (bnfItem.Isbn13 is null || !seenIsbns.Add(bnfItem.Isbn13))
            {
                continue;
            }

            merged.Add(openLibraryByIsbn.TryGetValue(bnfItem.Isbn13, out var enrichment)
                ? bnfItem with
                {
                    WorkId = bnfItem.WorkId ?? enrichment.WorkId,
                    Title = bnfItem.Title ?? enrichment.Title,
                    Authors = bnfItem.Authors ?? enrichment.Authors,
                    Publisher = bnfItem.Publisher ?? enrichment.Publisher,
                    PublicationYear = bnfItem.PublicationYear ?? enrichment.PublicationYear,
                    CoverUrl = bnfItem.CoverUrl ?? enrichment.CoverUrl,
                }
                : bnfItem);
        }

        foreach (var openLibraryItem in openLibraryItems)
        {
            if (openLibraryItem.Isbn13 is not null && !seenIsbns.Add(openLibraryItem.Isbn13))
            {
                continue;
            }

            merged.Add(openLibraryItem);
        }

        return merged;
    }

    private async Task<ProviderResult> TrySearchAsync(
        string provider,
        Func<Task<IReadOnlyList<BookReferenceSearchItem>>> searchAsync,
        CancellationToken cancellationToken)
    {
        try
        {
            return new ProviderResult(await searchAsync(), Failed: false);
        }
        catch (TaskCanceledException exception) when (!cancellationToken.IsCancellationRequested)
        {
            logger.LogWarning(exception, "{Source} reference search timed out.", provider);
        }
        catch (HttpRequestException exception)
        {
            logger.LogWarning(exception, "{Source} reference search failed.", provider);
        }
        catch (JsonException exception)
        {
            logger.LogWarning(exception, "{Source} returned an invalid reference search.", provider);
        }
        catch (XmlException exception)
        {
            logger.LogWarning(exception, "{Source} returned an invalid reference search.", provider);
        }

        return new ProviderResult([], Failed: true);
    }

    private sealed record ProviderResult(IReadOnlyList<BookReferenceSearchItem> Items, bool Failed);
}
