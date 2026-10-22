using MediatR;
using Microsoft.EntityFrameworkCore;
using Vole_Papillon_Damour.Application.Books.Common;
using Vole_Papillon_Damour.Application.Common.Interfaces.Persistence;
using Vole_Papillon_Damour.Application.Common.Interfaces.Services;
using Vole_Papillon_Damour.Domain.BookAggregate;
using Vole_Papillon_Damour.Domain.BookAggregate.Entities;
using Vole_Papillon_Damour.Domain.BookAggregate.ValueObjects;

namespace Vole_Papillon_Damour.Application.Books.Commands.Background;

public sealed class EnrichPendingBooksCommandHandler(
    IProjectDbContext dbContext,
    IBibliographicMetadataResolver metadataResolver,
    IDateTimeProvider dateTimeProvider,
    IBookRecommendationWorkQueue? recommendationWorkQueue = null)
    : IRequestHandler<EnrichPendingBooksCommand, EnrichPendingBooksResult>
{
    private static readonly TimeSpan InterIsbnDelay = TimeSpan.FromSeconds(1);
    private static readonly TimeSpan ProviderFailureRetryAfter = TimeSpan.FromHours(1);
    private static readonly TimeSpan NotFoundRetryAfterFirstAttempt = TimeSpan.FromDays(7);
    private static readonly TimeSpan NotFoundRetryAfterSecondAttempt = TimeSpan.FromDays(30);
    private static readonly TimeSpan CoverRetryAfter = TimeSpan.FromDays(30);
    private static readonly TimeSpan GenreRetryAfter = TimeSpan.FromDays(30);
    private static readonly TimeSpan BibliographicDetailsRetryAfter = TimeSpan.FromDays(30);

    public async Task<EnrichPendingBooksResult> Handle(
        EnrichPendingBooksCommand command,
        CancellationToken cancellationToken)
    {
        if (command.BatchSize <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(command.BatchSize));
        }

        var now = dateTimeProvider.UtcNow;
        if (now.Kind != DateTimeKind.Utc)
        {
            throw new InvalidOperationException("The worker clock must be expressed in UTC.");
        }

        var candidateQuery = dbContext.Books
            .AsNoTracking()
            .Where(book =>
                (book.MetadataStatus == BookMetadataStatus.Pending &&
                 (book.LastAttemptAt == null ||
                  book.LastAttemptAt <= now.Subtract(ProviderFailureRetryAfter))) ||
                (book.MetadataStatus == BookMetadataStatus.Failed &&
                 (book.LastAttemptAt == null ||
                  book.LastAttemptAt <= now.Subtract(ProviderFailureRetryAfter))) ||
                (book.MetadataStatus == BookMetadataStatus.NotFound &&
                 ((book.ResolveAttempts == 1 &&
                   book.LastAttemptAt <= now.Subtract(NotFoundRetryAfterFirstAttempt)) ||
                  (book.ResolveAttempts == 2 &&
                   book.LastAttemptAt <= now.Subtract(NotFoundRetryAfterSecondAttempt)))) ||
                (book.MetadataStatus == BookMetadataStatus.Resolved &&
                  book.CoverUrl == null &&
                  (book.CoverCheckedAt == null ||
                   book.CoverCheckedAt <= now.Subtract(CoverRetryAfter))) ||
                (command.IncludeMissingGenres &&
                 book.MetadataStatus == BookMetadataStatus.Resolved &&
                 book.Genre == null &&
                 (book.MetadataFetchedAt == null ||
                  book.MetadataFetchedAt <= now.Subtract(GenreRetryAfter)) &&
                 (book.LastAttemptAt == null ||
                  book.LastAttemptAt <= now.Subtract(GenreRetryAfter))) ||
                (command.RefreshAllGenres &&
                 book.MetadataStatus == BookMetadataStatus.Resolved &&
                 book.GenresRefreshedAt == null) ||
                (command.RefreshMissingBibliographicDetails &&
                 (book.MetadataStatus == BookMetadataStatus.Resolved ||
                  book.MetadataStatus == BookMetadataStatus.Manual) &&
                 (book.Summary == null || book.PhysicalFormat == null || book.Language == null) &&
                 (book.BibliographicDetailsRefreshedAt == null ||
                  book.BibliographicDetailsRefreshedAt <= now.Subtract(BibliographicDetailsRetryAfter))));

        if (command.Isbn13 is { } requestedIsbn13)
        {
            candidateQuery = candidateQuery.Where(book => book.Id == requestedIsbn13);
        }

        var candidates = await candidateQuery
            .OrderBy(book => book.LastAttemptAt)
            .ThenBy(book => book.Id)
            .Take(command.BatchSize)
            .Select(book => book.Id)
            .ToListAsync(cancellationToken);

        var resolvedCount = 0;
        var notFoundCount = 0;
        var failedCount = 0;
        var coverUpdatedCount = 0;
        var genreUpdatedCount = 0;
        var bibliographicDetailsUpdatedCount = 0;
        var hasProcessedCandidate = false;
        foreach (var isbn13 in candidates)
        {
            if (hasProcessedCandidate && command.Isbn13 is null)
            {
                await Task.Delay(InterIsbnDelay, cancellationToken);
            }

            hasProcessedCandidate = true;
            BookMetadataResult? metadata;
            try
            {
                metadata = await metadataResolver.ResolveAsync(
                    isbn13,
                    cancellationToken,
                    command.RefreshMissingBibliographicDetails);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception exception)
            {
                failedCount++;
                await RecordProviderFailureAsync(
                    isbn13,
                    now,
                    DescribeProviderFailure(exception),
                    command.IncludeMissingGenres,
                    command.RefreshAllGenres,
                    command.RefreshMissingBibliographicDetails,
                    cancellationToken);
                continue;
            }

            var book = await dbContext.Books
                .Include(candidate => candidate.Genres)
                .SingleOrDefaultAsync(candidate => candidate.Id == isbn13, cancellationToken);
            if (book is null)
            {
                continue;
            }

            if (book.MetadataStatus == BookMetadataStatus.Discarded)
            {
                continue;
            }

            var genreOnly = book.MetadataStatus == BookMetadataStatus.Resolved &&
                            ((command.IncludeMissingGenres && book.Genre is null) ||
                             (command.RefreshAllGenres && book.GenresRefreshedAt is null));
            var detailsOnly = command.RefreshMissingBibliographicDetails &&
                              (book.MetadataStatus is BookMetadataStatus.Resolved or BookMetadataStatus.Manual) &&
                              HasMissingBibliographicDetails(book) &&
                              (book.BibliographicDetailsRefreshedAt is null ||
                               book.BibliographicDetailsRefreshedAt <= now.Subtract(BibliographicDetailsRetryAfter));
            var coverOnly = !genreOnly &&
                            !detailsOnly &&
                            book.MetadataStatus == BookMetadataStatus.Resolved &&
                            book.CoverUrl is null &&
                            (!command.IncludeMissingGenres || book.Genre is not null);
            var existingBookRefresh =
                (book.MetadataStatus == BookMetadataStatus.Resolved && (coverOnly || genreOnly || detailsOnly)) ||
                (book.MetadataStatus == BookMetadataStatus.Manual && detailsOnly);
            if (!existingBookRefresh &&
                book.MetadataStatus is BookMetadataStatus.Manual or BookMetadataStatus.Resolved)
            {
                continue;
            }

            if (metadata is null)
            {
                if (existingBookRefresh)
                {
                    RecordExistingBookRefreshAttempt(
                        book,
                        command.IncludeMissingGenres,
                        command.RefreshAllGenres,
                        detailsOnly,
                        coverOnly,
                        now);
                }
                else
                {
                    book.RecordMetadataNotFound(now);
                    notFoundCount++;
                }

                await dbContext.SaveChangesAsync(cancellationToken);
                continue;
            }

            if (!TryMapSource(metadata.Source, out var source) ||
                !string.Equals(metadata.Isbn13, isbn13.Value, StringComparison.Ordinal))
            {
                failedCount++;
                if (existingBookRefresh)
                {
                    RecordExistingBookRefreshAttempt(
                        book,
                        command.IncludeMissingGenres,
                        command.RefreshAllGenres,
                        detailsOnly,
                        coverOnly,
                        now);
                }
                else
                {
                    book.RecordMetadataProviderFailure(
                        now,
                        "La source a renvoyé un autre ISBN que celui demandé.");
                }

                await dbContext.SaveChangesAsync(cancellationToken);
                continue;
            }

            var includeCoreMetadata = !existingBookRefresh && !coverOnly;
            var includeGenres = !coverOnly && (!existingBookRefresh || genreOnly);
            var includeDetails = !coverOnly && (!existingBookRefresh || detailsOnly);
            var includeCover = coverOnly || !existingBookRefresh;
            var patch = CreatePatch(
                metadata,
                book,
                includeCoreMetadata,
                includeGenres,
                includeDetails,
                includeCover);
            if (patch.Fields.Count == 0)
            {
                if (existingBookRefresh)
                {
                    RecordExistingBookRefreshAttempt(
                        book,
                        command.IncludeMissingGenres,
                        command.RefreshAllGenres,
                        detailsOnly,
                        coverOnly,
                        now);
                }
                else
                {
                    failedCount++;
                    book.RecordMetadataProviderFailure(
                        now,
                        "La notice ne contient aucune métadonnée exploitable.");
                }

                await dbContext.SaveChangesAsync(cancellationToken);
                continue;
            }

            BookCoverSource? coverSource = null;
            var mappedCoverSource = default(BookCoverSource);
            if (patch.CoverUrl is not null &&
                !TryMapCoverSource(metadata.CoverSource, metadata.Source, out mappedCoverSource))
            {
                failedCount++;
                if (existingBookRefresh)
                {
                    RecordExistingBookRefreshAttempt(
                        book,
                        command.IncludeMissingGenres,
                        command.RefreshAllGenres,
                        detailsOnly,
                        coverOnly,
                        now);
                }
                else
                {
                    book.RecordMetadataProviderFailure(
                        now,
                        "La notice indique une source de couverture non reconnue.");
                }

                await dbContext.SaveChangesAsync(cancellationToken);
                continue;
            }
            coverSource = patch.CoverUrl is null ? null : mappedCoverSource;

            var wasResolved = book.MetadataStatus == BookMetadataStatus.Resolved;
            var hadCover = book.CoverUrl is not null;
            var hadGenre = !string.IsNullOrWhiteSpace(book.Genre);
            var previousSummary = book.Summary;
            var previousPhysicalFormat = book.PhysicalFormat;
            var previousLanguage = book.Language;
            try
            {
                book.ApplyAutomaticMetadata(
                    patch,
                    source,
                    metadata.RetrievedAt.UtcDateTime,
                    rawPayload: null,
                    coverSource: patch.CoverUrl is null ? null : coverSource,
                    coverCheckedAt: patch.Fields.Contains(BookMetadataField.CoverUrl) || !existingBookRefresh
                        ? metadata.RetrievedAt.UtcDateTime
                        : null);
                if (!patch.Fields.Contains(BookMetadataField.Genre))
                {
                    RecordGenreRefreshAttempt(
                        book,
                        command.IncludeMissingGenres,
                        command.RefreshAllGenres,
                        now);
                }

                if (detailsOnly)
                {
                    book.RecordBibliographicDetailsRefreshAttempt(now);
                }

                if (!hadGenre && !string.IsNullOrWhiteSpace(book.Genre))
                {
                    genreUpdatedCount++;
                }

                if (previousSummary != book.Summary ||
                    previousPhysicalFormat != book.PhysicalFormat ||
                    previousLanguage != book.Language)
                {
                    bibliographicDetailsUpdatedCount++;
                }
            }
            catch (ArgumentException)
            {
                failedCount++;
                if (existingBookRefresh)
                {
                    RecordExistingBookRefreshAttempt(
                        book,
                        command.IncludeMissingGenres,
                        command.RefreshAllGenres,
                        detailsOnly,
                        coverOnly,
                        now);
                }
                else
                {
                    book.RecordMetadataProviderFailure(
                        now,
                        "Les métadonnées de la notice ne peuvent pas être enregistrées.");
                }

                await dbContext.SaveChangesAsync(cancellationToken);
                continue;
            }

            if (!hadCover && patch.CoverUrl is not null)
            {
                coverUpdatedCount++;
            }

            if (!existingBookRefresh && !coverOnly && string.IsNullOrWhiteSpace(book.Title))
            {
                failedCount++;
                book.RecordMetadataProviderFailure(
                    now,
                    "La notice ne contient pas de titre exploitable.",
                    countAttempt: false);
            }
            else if (!existingBookRefresh && !coverOnly && !wasResolved)
            {
                resolvedCount++;
            }

            var recommendationInputChanged = previousSummary != book.Summary ||
                                             (!wasResolved &&
                                              book.MetadataStatus is (BookMetadataStatus.Resolved or BookMetadataStatus.Manual));
            var recommendationJob = recommendationInputChanged && recommendationWorkQueue is not null
                ? await dbContext.BookRecommendationJobs
                    .SingleOrDefaultAsync(job => job.Isbn13 == isbn13.Value, cancellationToken)
                : null;
            if (recommendationJob is not null &&
                recommendationJob.Request(now, force: true))
            {
                await dbContext.SaveChangesAsync(cancellationToken);
                await recommendationWorkQueue!.EnqueueBooksAsync(
                    [new BookRecommendationQueueItem(recommendationJob.Isbn13, recommendationJob.RequestVersion)],
                    cancellationToken);
                continue;
            }

            await dbContext.SaveChangesAsync(cancellationToken);
        }

        return new EnrichPendingBooksResult(
            candidates.Count,
            resolvedCount,
            notFoundCount,
            failedCount,
            coverUpdatedCount,
            genreUpdatedCount,
            bibliographicDetailsUpdatedCount);
    }

    private async Task RecordProviderFailureAsync(
        Isbn13 isbn13,
        DateTime attemptedAt,
        string reason,
        bool includeMissingGenres,
        bool refreshAllGenres,
        bool refreshMissingBibliographicDetails,
        CancellationToken cancellationToken)
    {
        var book = await dbContext.Books
            .SingleOrDefaultAsync(candidate => candidate.Id == isbn13, cancellationToken);
        if (book is null)
        {
            return;
        }

        if (book.MetadataStatus is BookMetadataStatus.Resolved or BookMetadataStatus.Manual)
        {
            var changed = false;
            if (book.MetadataStatus == BookMetadataStatus.Resolved && book.CoverUrl is null)
            {
                changed |= book.RecordCoverCheck(attemptedAt);
            }

            changed |= RecordGenreRefreshAttempt(
                book,
                includeMissingGenres,
                refreshAllGenres,
                attemptedAt);
            if (refreshMissingBibliographicDetails && HasMissingBibliographicDetails(book))
            {
                changed |= book.RecordBibliographicDetailsRefreshAttempt(attemptedAt);
            }

            if (changed)
            {
                await dbContext.SaveChangesAsync(cancellationToken);
            }

            return;
        }

        if (book.MetadataStatus == BookMetadataStatus.Manual)
        {
            return;
        }

        if (book.RecordMetadataProviderFailure(attemptedAt, reason))
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
    }

    private static string DescribeProviderFailure(Exception exception)
    {
        var detail = string.IsNullOrWhiteSpace(exception.Message)
            ? exception.GetType().Name
            : $"{exception.GetType().Name} : {exception.Message.Trim()}";
        return $"Échec technique de la recherche bibliographique — {detail}";
    }

    private static BookMetadataPatch CreatePatch(
        BookMetadataResult metadata,
        Book book,
        bool includeCoreMetadata,
        bool includeGenres,
        bool includeDetails,
        bool includeCover)
    {
        var title = includeCoreMetadata ? Clean(metadata.Title) : null;
        var authors = includeCoreMetadata ? Clean(metadata.Authors) : null;
        var publisher = includeCoreMetadata ? Clean(metadata.Publisher) : null;
        var genres = includeGenres ? CleanGenres(metadata.Genres, metadata.Genre) : [];
        var genre = genres.FirstOrDefault();
        var workId = includeCoreMetadata ? Clean(metadata.WorkId) : null;
        var coverUrl = includeCover ? metadata.CoverUrl?.ToString() : null;
        var existingBook = book.MetadataStatus is BookMetadataStatus.Resolved or BookMetadataStatus.Manual;
        var summary = includeDetails ? BookSummaryText.Clean(metadata.Summary, metadata.Title) : null;
        var physicalFormat = includeDetails ? Clean(metadata.PhysicalFormat) : null;
        var language = includeDetails ? Clean(metadata.Language) : null;
        if (existingBook)
        {
            if (book.Summary is not null || book.IsMetadataFieldManuallyEdited(BookMetadataField.Summary))
            {
                summary = null;
            }

            if (book.PhysicalFormat is not null || book.IsMetadataFieldManuallyEdited(BookMetadataField.PhysicalFormat))
            {
                physicalFormat = null;
            }

            if (book.Language is not null || book.IsMetadataFieldManuallyEdited(BookMetadataField.Language))
            {
                language = null;
            }
        }

        if (physicalFormat?.Length > 50) physicalFormat = null;
        if (language?.Length > 10) language = null;
        var fields = new List<BookMetadataField>();

        if (title is not null) fields.Add(BookMetadataField.Title);
        if (authors is not null) fields.Add(BookMetadataField.Authors);
        if (publisher is not null) fields.Add(BookMetadataField.Publisher);
        if (includeCoreMetadata && metadata.PublicationYear is not null) fields.Add(BookMetadataField.PublicationYear);
        if (genre is not null) fields.Add(BookMetadataField.Genre);
        if (workId is not null) fields.Add(BookMetadataField.WorkId);
        if (coverUrl is not null) fields.Add(BookMetadataField.CoverUrl);
        if (physicalFormat is not null) fields.Add(BookMetadataField.PhysicalFormat);
        if (language is not null) fields.Add(BookMetadataField.Language);
        if (summary is not null) fields.Add(BookMetadataField.Summary);

        return new BookMetadataPatch(
            title,
            authors,
            publisher,
            includeCoreMetadata ? metadata.PublicationYear : null,
            PhysicalFormat: physicalFormat,
            Language: language,
            Genre: genre,
            CoverUrl: coverUrl,
            fields,
            workId,
            genres,
            summary);
    }

    private static string? Clean(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    private static IReadOnlyCollection<string> CleanGenres(
        IReadOnlyList<string>? values,
        string? legacyGenre)
    {
        var allValues = (values ?? [])
            .Concat(legacyGenre is null ? [] : [legacyGenre]);
        var genres = new List<string>();
        foreach (var value in allValues)
        {
            var cleaned = Clean(value);
            if (cleaned is not null &&
                cleaned.Length <= 100 &&
                !genres.Any(existing => BookGenre.NamesAreEquivalent(existing, cleaned)))
            {
                genres.Add(cleaned);
            }
        }

        return genres;
    }

    private static void RecordExistingBookRefreshAttempt(
        Book book,
        bool includeMissingGenres,
        bool refreshAllGenres,
        bool refreshBibliographicDetails,
        bool coverOnly,
        DateTime attemptedAt)
    {
        if (coverOnly)
        {
            book.RecordCoverCheck(attemptedAt);
        }

        RecordGenreRefreshAttempt(book, includeMissingGenres, refreshAllGenres, attemptedAt);
        if (refreshBibliographicDetails)
        {
            book.RecordBibliographicDetailsRefreshAttempt(attemptedAt);
        }
    }

    private static bool RecordGenreRefreshAttempt(
        Book book,
        bool includeMissingGenres,
        bool refreshAllGenres,
        DateTime attemptedAt)
    {
        if (refreshAllGenres && book.MetadataStatus == BookMetadataStatus.Resolved)
        {
            return book.RecordGenreRefreshAttempt(attemptedAt);
        }

        return includeMissingGenres &&
               book.MetadataStatus == BookMetadataStatus.Resolved &&
               book.Genre is null &&
               book.RecordMetadataRefreshAttempt(attemptedAt);
    }

    private static bool HasMissingBibliographicDetails(Book book)
    {
        return book.Summary is null || book.PhysicalFormat is null || book.Language is null;
    }

    private static bool TryMapSource(string source, out BookMetadataSource mappedSource)
    {
        if (string.Equals(source, "BnF", StringComparison.OrdinalIgnoreCase))
        {
            mappedSource = BookMetadataSource.Bnf;
            return true;
        }

        if (string.Equals(source, "OpenLibrary", StringComparison.OrdinalIgnoreCase))
        {
            mappedSource = BookMetadataSource.OpenLibrary;
            return true;
        }

        if (string.Equals(source, "GoogleBooks", StringComparison.OrdinalIgnoreCase))
        {
            mappedSource = BookMetadataSource.GoogleBooks;
            return true;
        }

        mappedSource = default;
        return false;
    }

    private static bool TryMapCoverSource(
        string? coverSource,
        string metadataSource,
        out BookCoverSource mappedSource)
    {
        var source = string.IsNullOrWhiteSpace(coverSource) ? metadataSource : coverSource;
        if (string.Equals(source, "BnF", StringComparison.OrdinalIgnoreCase))
        {
            mappedSource = BookCoverSource.Bnf;
            return true;
        }

        if (string.Equals(source, "OpenLibrary", StringComparison.OrdinalIgnoreCase))
        {
            mappedSource = BookCoverSource.OpenLibrary;
            return true;
        }

        if (string.Equals(source, "GoogleBooks", StringComparison.OrdinalIgnoreCase))
        {
            mappedSource = BookCoverSource.GoogleBooks;
            return true;
        }

        mappedSource = default;
        return false;
    }
}
