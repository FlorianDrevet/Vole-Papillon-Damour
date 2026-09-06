using System.Text.Json;
using System.Xml;
using Microsoft.Extensions.Logging;
using Vole_Papillon_Damour.Application.Books.Common;
using Vole_Papillon_Damour.Application.Common.Interfaces.Services;
using Vole_Papillon_Damour.Domain.BookAggregate.ValueObjects;

namespace Vole_Papillon_Damour.Infrastructure.Services.Bibliographic;

public sealed class BibliographicMetadataResolver(
    IBnfSruClient bnfClient,
    IOpenLibraryClient openLibraryClient,
    IGoogleBooksClient googleBooksClient,
    ILogger<BibliographicMetadataResolver> logger) : IBibliographicMetadataResolver
{
    public async Task<BookMetadataResult?> ResolveAsync(
        Isbn13 isbn13,
        CancellationToken cancellationToken)
    {
        var bnfResult = await TryFindAsync(
            bnfClient,
            "BnF",
            isbn13,
            cancellationToken);
        if (bnfResult.Metadata is not null)
        {
            var openLibraryResult = ProviderResult.Empty;
            if (bnfResult.Metadata.CoverUrl is null ||
                string.IsNullOrWhiteSpace(bnfResult.Metadata.WorkId))
            {
                openLibraryResult = await TryFindAsync(
                    openLibraryClient,
                    "OpenLibrary",
                    isbn13,
                    cancellationToken);
            }

            var merged = Merge(bnfResult.Metadata, openLibraryResult.Metadata);
            if (merged.CoverUrl is not null)
            {
                return merged;
            }

            var googleBooksResult = await TryFindAsync(
                googleBooksClient,
                "GoogleBooks",
                isbn13,
                cancellationToken);
            return googleBooksResult.Metadata is null
                ? merged
                : Merge(merged, googleBooksResult.Metadata);
        }

        var openLibraryFallbackResult = await TryFindAsync(
            openLibraryClient,
            "OpenLibrary",
            isbn13,
            cancellationToken);
        if (openLibraryFallbackResult.Metadata is not null)
        {
            if (openLibraryFallbackResult.Metadata.CoverUrl is not null)
            {
                return openLibraryFallbackResult.Metadata;
            }

            var googleBooksFallbackResult = await TryFindAsync(
                googleBooksClient,
                "GoogleBooks",
                isbn13,
                cancellationToken);
            return googleBooksFallbackResult.Metadata is null
                ? openLibraryFallbackResult.Metadata
                : Merge(openLibraryFallbackResult.Metadata, googleBooksFallbackResult.Metadata);
        }

        var googleBooksOnlyResult = await TryFindAsync(
            googleBooksClient,
            "GoogleBooks",
            isbn13,
            cancellationToken);
        if (googleBooksOnlyResult.Metadata is not null)
        {
            return googleBooksOnlyResult.Metadata;
        }

        if (bnfResult.Failed ||
            openLibraryFallbackResult.Failed ||
            googleBooksOnlyResult.Failed)
        {
            throw new HttpRequestException(
                $"All bibliographic providers that were contacted failed for ISBN {isbn13.Value}.");
        }

        return null;
    }

    private async Task<ProviderResult> TryFindAsync(
        object client,
        string source,
        Isbn13 isbn13,
        CancellationToken cancellationToken)
    {
        try
        {
            return new ProviderResult(client switch
            {
                IBnfSruClient bnf => await bnf.FindAsync(isbn13, cancellationToken),
                IOpenLibraryClient openLibrary => await openLibrary.FindAsync(isbn13, cancellationToken),
                IGoogleBooksClient googleBooks => await googleBooks.FindAsync(isbn13, cancellationToken),
                _ => null
            }, Failed: false);
        }
        catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            logger.LogWarning("{Source} metadata lookup timed out for ISBN {Isbn13}.", source, isbn13.Value);
            return new ProviderResult(null, Failed: true);
        }
        catch (HttpRequestException exception)
        {
            logger.LogWarning(exception, "{Source} metadata lookup failed for ISBN {Isbn13}.", source, isbn13.Value);
            return new ProviderResult(null, Failed: true);
        }
        catch (JsonException exception)
        {
            logger.LogWarning(exception, "{Source} returned invalid metadata for ISBN {Isbn13}.", source, isbn13.Value);
            return new ProviderResult(null, Failed: true);
        }
        catch (XmlException exception)
        {
            logger.LogWarning(exception, "{Source} returned invalid metadata for ISBN {Isbn13}.", source, isbn13.Value);
            return new ProviderResult(null, Failed: true);
        }
    }

    private static BookMetadataResult Merge(
        BookMetadataResult primary,
        BookMetadataResult? enrichment)
    {
        if (enrichment is null)
        {
            return primary;
        }

        var coverUrl = primary.CoverUrl ?? enrichment.CoverUrl;
        var coverSource = primary.CoverUrl is not null
            ? primary.CoverSource
            : enrichment.CoverUrl is not null
                ? enrichment.CoverSource ?? enrichment.Source
                : primary.CoverSource;

        return primary with
        {
            WorkId = primary.WorkId ?? enrichment.WorkId,
            CoverUrl = coverUrl,
            CoverSource = coverSource,
        };
    }

    private sealed record ProviderResult(BookMetadataResult? Metadata, bool Failed)
    {
        public static ProviderResult Empty { get; } = new(null, Failed: false);
    }
}
