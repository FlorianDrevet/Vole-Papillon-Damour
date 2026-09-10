using System.Diagnostics;
using System.Text.Json;
using System.Xml;
using Microsoft.Extensions.Logging;
using Vole_Papillon_Damour.Application.Books.Common;
using Vole_Papillon_Damour.Application.Common.Interfaces.Services;
using Vole_Papillon_Damour.Application.Common.Observability;
using Vole_Papillon_Damour.Domain.BookAggregate.ValueObjects;

namespace Vole_Papillon_Damour.Infrastructure.Services.Bibliographic;

public sealed class BibliographicMetadataResolver(
    IBnfSruClient bnfClient,
    IOpenLibraryClient openLibraryClient,
    IGoogleBooksClient googleBooksClient,
    ILogger<BibliographicMetadataResolver> logger) : IBibliographicMetadataResolver
{
    private const string BnfProvider = "BnF";
    private const string OpenLibraryProvider = "OpenLibrary";
    private const string GoogleBooksProvider = "GoogleBooks";

    public async Task<BookMetadataResult?> ResolveAsync(
        Isbn13 isbn13,
        CancellationToken cancellationToken)
    {
        using var activity = BookScanTelemetry.StartActivity(
            BookScanTelemetry.MetadataResolutionActivityName);
        activity?.SetTag(BookScanTelemetry.BookIsbnTagName, isbn13.Value);

        var stopwatch = Stopwatch.StartNew();
        var outcome = BookScanTelemetry.FailedOutcome;
        BookMetadataResult? metadata = null;

        try
        {
            metadata = await ResolveCoreAsync(isbn13, cancellationToken);
            outcome = metadata is null
                ? BookScanTelemetry.NotFoundOutcome
                : BookScanTelemetry.FoundOutcome;
            activity?.SetStatus(
                metadata is null ? ActivityStatusCode.Unset : ActivityStatusCode.Ok);
            return metadata;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            outcome = BookScanTelemetry.CancelledOutcome;
            activity?.SetStatus(ActivityStatusCode.Error);
            throw;
        }
        catch
        {
            activity?.SetStatus(ActivityStatusCode.Error);
            throw;
        }
        finally
        {
            activity?.SetTag(BookScanTelemetry.MetadataOutcomeTagName, outcome);
            activity?.SetTag(
                BookScanTelemetry.MetadataSourceTagName,
                metadata?.Source ?? "none");
            BookScanTelemetry.RecordMetadataResolutionDuration(
                stopwatch.Elapsed,
                outcome,
                metadata?.Source);
        }
    }

    private async Task<BookMetadataResult?> ResolveCoreAsync(
        Isbn13 isbn13,
        CancellationToken cancellationToken)
    {
        var bnfResult = await TryFindAsync(
            BnfProvider,
            () => bnfClient.FindAsync(isbn13, cancellationToken),
            isbn13,
            cancellationToken);
        if (bnfResult.Metadata is not null)
        {
            var openLibraryResult = ProviderResult.Empty;
            if (bnfResult.Metadata.CoverUrl is null ||
                string.IsNullOrWhiteSpace(bnfResult.Metadata.WorkId) ||
                string.IsNullOrWhiteSpace(bnfResult.Metadata.Genre))
            {
                openLibraryResult = await TryFindAsync(
                    OpenLibraryProvider,
                    () => openLibraryClient.FindAsync(isbn13, cancellationToken),
                    isbn13,
                    cancellationToken);
            }

            var merged = Merge(bnfResult.Metadata, openLibraryResult.Metadata);
            if (HasCompleteMetadata(merged))
            {
                return merged;
            }

            var googleBooksResult = await TryFindAsync(
                GoogleBooksProvider,
                () => googleBooksClient.FindAsync(isbn13, cancellationToken),
                isbn13,
                cancellationToken);
            return googleBooksResult.Metadata is null
                ? merged
                : Merge(merged, googleBooksResult.Metadata);
        }

        var openLibraryFallbackResult = await TryFindAsync(
            OpenLibraryProvider,
            () => openLibraryClient.FindAsync(isbn13, cancellationToken),
            isbn13,
            cancellationToken);
        if (openLibraryFallbackResult.Metadata is not null)
        {
            if (HasCompleteMetadata(openLibraryFallbackResult.Metadata))
            {
                return openLibraryFallbackResult.Metadata;
            }

            var googleBooksFallbackResult = await TryFindAsync(
                GoogleBooksProvider,
                () => googleBooksClient.FindAsync(isbn13, cancellationToken),
                isbn13,
                cancellationToken);
            return googleBooksFallbackResult.Metadata is null
                ? openLibraryFallbackResult.Metadata
                : Merge(openLibraryFallbackResult.Metadata, googleBooksFallbackResult.Metadata);
        }

        var googleBooksOnlyResult = await TryFindAsync(
            GoogleBooksProvider,
            () => googleBooksClient.FindAsync(isbn13, cancellationToken),
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
        string provider,
        Func<Task<BookMetadataResult?>> findAsync,
        Isbn13 isbn13,
        CancellationToken cancellationToken)
    {
        using var activity = BookScanTelemetry.StartActivity(
            BookScanTelemetry.MetadataProviderActivityName);
        activity?.SetTag(BookScanTelemetry.BookIsbnTagName, isbn13.Value);
        activity?.SetTag(BookScanTelemetry.MetadataProviderTagName, provider);

        var stopwatch = Stopwatch.StartNew();
        var outcome = BookScanTelemetry.FailedOutcome;

        try
        {
            var metadata = await findAsync();
            outcome = metadata is null
                ? BookScanTelemetry.NotFoundOutcome
                : BookScanTelemetry.FoundOutcome;
            activity?.SetStatus(
                metadata is null ? ActivityStatusCode.Unset : ActivityStatusCode.Ok);
            return new ProviderResult(metadata, Failed: false);
        }
        catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            outcome = BookScanTelemetry.TimeoutOutcome;
            activity?.SetStatus(ActivityStatusCode.Error);
            logger.LogWarning(
                "{Source} metadata lookup timed out for ISBN {Isbn13}.",
                provider,
                isbn13.Value);
            return new ProviderResult(null, Failed: true);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            outcome = BookScanTelemetry.CancelledOutcome;
            activity?.SetStatus(ActivityStatusCode.Error);
            throw;
        }
        catch (HttpRequestException exception)
        {
            activity?.SetStatus(ActivityStatusCode.Error);
            logger.LogWarning(
                exception,
                "{Source} metadata lookup failed for ISBN {Isbn13}.",
                provider,
                isbn13.Value);
            return new ProviderResult(null, Failed: true);
        }
        catch (JsonException exception)
        {
            activity?.SetStatus(ActivityStatusCode.Error);
            logger.LogWarning(
                exception,
                "{Source} returned invalid metadata for ISBN {Isbn13}.",
                provider,
                isbn13.Value);
            return new ProviderResult(null, Failed: true);
        }
        catch (XmlException exception)
        {
            activity?.SetStatus(ActivityStatusCode.Error);
            logger.LogWarning(
                exception,
                "{Source} returned invalid metadata for ISBN {Isbn13}.",
                provider,
                isbn13.Value);
            return new ProviderResult(null, Failed: true);
        }
        finally
        {
            activity?.SetTag(BookScanTelemetry.MetadataOutcomeTagName, outcome);
            BookScanTelemetry.RecordMetadataProviderDuration(
                stopwatch.Elapsed,
                provider,
                outcome);
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
            Genre = string.IsNullOrWhiteSpace(primary.Genre)
                ? enrichment.Genre
                : primary.Genre,
        };
    }

    private static bool HasCompleteMetadata(BookMetadataResult metadata)
    {
        return metadata.CoverUrl is not null &&
               !string.IsNullOrWhiteSpace(metadata.Genre);
    }

    private sealed record ProviderResult(BookMetadataResult? Metadata, bool Failed)
    {
        public static ProviderResult Empty { get; } = new(null, Failed: false);
    }
}
