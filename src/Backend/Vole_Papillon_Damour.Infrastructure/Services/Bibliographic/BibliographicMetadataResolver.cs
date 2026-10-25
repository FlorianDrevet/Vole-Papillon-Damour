using System.Diagnostics;
using System.Text.Json;
using System.Xml;
using Microsoft.Extensions.Logging;
using Vole_Papillon_Damour.Application.Books.Common;
using Vole_Papillon_Damour.Application.Common.Interfaces.Services;
using Vole_Papillon_Damour.Application.Common.Observability;
using Vole_Papillon_Damour.Application.Isbndb;
using Vole_Papillon_Damour.Domain.BookAggregate.ValueObjects;
using Vole_Papillon_Damour.Domain.IsbndbAggregate;

namespace Vole_Papillon_Damour.Infrastructure.Services.Bibliographic;

public sealed class BibliographicMetadataResolver(
    IBnfSruClient bnfClient,
    IOpenLibraryClient openLibraryClient,
    IGoogleBooksClient googleBooksClient,
    ILogger<BibliographicMetadataResolver> logger,
    IIsbndbLookupService? isbndbLookupService = null) : IBibliographicMetadataResolver
{
    private const string BnfProvider = "BnF";
    private const string OpenLibraryProvider = "OpenLibrary";
    private const string GoogleBooksProvider = "GoogleBooks";

    public async Task<BookMetadataResult?> ResolveAsync(
        Isbn13 isbn13,
        CancellationToken cancellationToken,
        bool includeBibliographicDetails = false,
        IsbndbUsage usage = IsbndbUsage.Scan,
        IsbndbLookup? prefetchedIsbndbLookup = null)
    {
        using var activity = BookScanTelemetry.StartActivity(
            BookScanTelemetry.MetadataResolutionActivityName);
        activity?.SetTag(BookScanTelemetry.BookIsbnTagName, isbn13.Value);

        var stopwatch = Stopwatch.StartNew();
        var outcome = BookScanTelemetry.FailedOutcome;
        BookMetadataResult? metadata = null;

        try
        {
            metadata = await ResolveCoreAsync(
                isbn13,
                cancellationToken,
                includeBibliographicDetails,
                usage,
                prefetchedIsbndbLookup);
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

    public async Task<string?> ResolveWorkIdAsync(Isbn13 isbn13, CancellationToken cancellationToken)
    {
        var bnfResult = await TryFindAsync(
            BnfProvider,
            () => bnfClient.FindAsync(isbn13, cancellationToken),
            isbn13,
            cancellationToken);
        if (!string.IsNullOrWhiteSpace(bnfResult.Metadata?.WorkId))
        {
            return bnfResult.Metadata.WorkId.Trim();
        }

        var openLibraryResult = await TryFindAsync(
            OpenLibraryProvider,
            () => openLibraryClient.FindAsync(
                isbn13,
                cancellationToken,
                includeBibliographicDetails: false),
            isbn13,
            cancellationToken);
        return string.IsNullOrWhiteSpace(openLibraryResult.Metadata?.WorkId)
            ? null
            : openLibraryResult.Metadata.WorkId.Trim();
    }

    private async Task<BookMetadataResult?> ResolveCoreAsync(
        Isbn13 isbn13,
        CancellationToken cancellationToken,
        bool includeBibliographicDetails,
        IsbndbUsage usage,
        IsbndbLookup? prefetchedIsbndbLookup)
    {
        var isbndbLookup = prefetchedIsbndbLookup;
        if (isbndbLookup is null && isbndbLookupService is not null)
        {
            isbndbLookup = await isbndbLookupService.LookupAsync(isbn13, usage, cancellationToken);
        }

        if (isbndbLookup is IsbndbLookup.Found found)
        {
            return MapIsbndbNotice(isbn13, found.Book);
        }

        var isbndbOutcome = isbndbLookup switch
        {
            IsbndbLookup.NotFound => IsbndbOutcome.NotFound,
            IsbndbLookup.QuotaExhausted or IsbndbLookup.Unavailable => IsbndbOutcome.Deferred,
            _ => (IsbndbOutcome?)null
        };
        var fallback = await ResolveFallbackAsync(isbn13, cancellationToken, includeBibliographicDetails);
        return isbndbOutcome is null || fallback is null
            ? fallback
            : fallback with { IsbndbOutcome = isbndbOutcome };
    }

    private async Task<BookMetadataResult?> ResolveFallbackAsync(
        Isbn13 isbn13,
        CancellationToken cancellationToken,
        bool includeBibliographicDetails)
    {
        var bnfResult = await TryFindAsync(
            BnfProvider,
            () => bnfClient.FindAsync(isbn13, cancellationToken),
            isbn13,
            cancellationToken);
        if (bnfResult.Metadata is not null)
        {
            var merged = bnfResult.Metadata;
            if (NeedsOpenLibrary(merged, includeBibliographicDetails))
            {
                var openLibraryResult = await TryFindAsync(
                    OpenLibraryProvider,
                    () => openLibraryClient.FindAsync(
                        isbn13,
                        cancellationToken,
                        includeBibliographicDetails),
                    isbn13,
                    cancellationToken);
                merged = Merge(merged, openLibraryResult.Metadata);
            }

            return await FillFromGoogleBooksIfNeededAsync(
                merged,
                isbn13,
                cancellationToken,
                includeBibliographicDetails);
        }

        var openLibraryFallbackResult = await TryFindAsync(
            OpenLibraryProvider,
            () => openLibraryClient.FindAsync(
                isbn13,
                cancellationToken,
                includeBibliographicDetails),
            isbn13,
            cancellationToken);
        if (openLibraryFallbackResult.Metadata is not null)
        {
            return await FillFromGoogleBooksIfNeededAsync(
                openLibraryFallbackResult.Metadata,
                isbn13,
                cancellationToken,
                includeBibliographicDetails);
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

    private static BookMetadataResult MapIsbndbNotice(Isbn13 isbn13, IsbndbBook book)
    {
        var patch = IsbndbNoticeMapper.ToPatch(book);
        var coverUrl = Uri.TryCreate(patch.CoverUrl, UriKind.Absolute, out var cover)
            ? cover
            : null;
        return new BookMetadataResult(
            isbn13.Value,
            patch.Title,
            patch.Authors,
            patch.Publisher,
            patch.PublicationYear,
            coverUrl,
            "ISBNdb",
            WorkId: null,
            RetrievedAt: DateTimeOffset.UtcNow,
            CoverSource: coverUrl is null ? null : "ISBNdb",
            Genre: patch.Genre,
            Genres: patch.Genres?.ToArray(),
            Summary: patch.Summary,
            PhysicalFormat: patch.PhysicalFormat,
            Language: patch.Language,
            IsbndbOutcome: IsbndbOutcome.Found,
            IsbndbNotice: book,
            Pages: patch.Pages,
            Edition: patch.Edition,
            WorkKey: WorkKey.Compute(patch.Title, patch.Authors));
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

        var genres = MergeGenres(primary, enrichment);
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
            Genre = genres.FirstOrDefault(),
            Genres = genres,
            Summary = primary.Summary ?? enrichment.Summary,
            PhysicalFormat = primary.PhysicalFormat ?? enrichment.PhysicalFormat,
            Language = primary.Language ?? enrichment.Language,
        };
    }

    private static IReadOnlyList<string> MergeGenres(
        BookMetadataResult primary,
        BookMetadataResult enrichment)
    {
        return new[] {primary.Genres, enrichment.Genres}
            .Where(values => values is not null)
            .SelectMany(values => values!)
            .Concat([primary.Genre, enrichment.Genre])
            .Where(genre => !string.IsNullOrWhiteSpace(genre))
            .Select(genre => string.Join(' ', genre!.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static bool HasCompleteMetadata(
        BookMetadataResult metadata,
        bool includeBibliographicDetails)
    {
        return metadata.CoverUrl is not null &&
               (metadata.Genres?.Count > 0 || !string.IsNullOrWhiteSpace(metadata.Genre)) &&
               (!includeBibliographicDetails ||
                (!string.IsNullOrWhiteSpace(metadata.Summary) &&
                 !string.IsNullOrWhiteSpace(metadata.Language)));
    }

    private static bool NeedsOpenLibrary(
        BookMetadataResult metadata,
        bool includeBibliographicDetails)
    {
        return metadata.CoverUrl is null ||
               string.IsNullOrWhiteSpace(metadata.WorkId) ||
               (metadata.Genres is not { Count: > 0 } && string.IsNullOrWhiteSpace(metadata.Genre)) ||
               (includeBibliographicDetails &&
                (string.IsNullOrWhiteSpace(metadata.Summary) ||
                 string.IsNullOrWhiteSpace(metadata.PhysicalFormat) ||
                 string.IsNullOrWhiteSpace(metadata.Language)));
    }

    private async Task<BookMetadataResult> FillFromGoogleBooksIfNeededAsync(
        BookMetadataResult metadata,
        Isbn13 isbn13,
        CancellationToken cancellationToken,
        bool includeBibliographicDetails)
    {
        if (HasCompleteMetadata(metadata, includeBibliographicDetails))
        {
            return metadata;
        }

        var googleBooksResult = await TryFindAsync(
            GoogleBooksProvider,
            () => googleBooksClient.FindAsync(isbn13, cancellationToken),
            isbn13,
            cancellationToken);
        return googleBooksResult.Metadata is null
            ? metadata
            : Merge(metadata, googleBooksResult.Metadata);
    }

    private sealed record ProviderResult(BookMetadataResult? Metadata, bool Failed)
    {
        public static ProviderResult Empty { get; } = new(null, Failed: false);
    }
}
