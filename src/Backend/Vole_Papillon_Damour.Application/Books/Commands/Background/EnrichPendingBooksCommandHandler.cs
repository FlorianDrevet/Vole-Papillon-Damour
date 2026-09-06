using MediatR;
using Microsoft.EntityFrameworkCore;
using Vole_Papillon_Damour.Application.Books.Common;
using Vole_Papillon_Damour.Application.Common.Interfaces.Persistence;
using Vole_Papillon_Damour.Application.Common.Interfaces.Services;
using Vole_Papillon_Damour.Domain.BookAggregate.ValueObjects;

namespace Vole_Papillon_Damour.Application.Books.Commands.Background;

public sealed class EnrichPendingBooksCommandHandler(
    IProjectDbContext dbContext,
    IBibliographicMetadataResolver metadataResolver,
    IDateTimeProvider dateTimeProvider)
    : IRequestHandler<EnrichPendingBooksCommand, EnrichPendingBooksResult>
{
    private static readonly TimeSpan ProviderFailureRetryAfter = TimeSpan.FromHours(1);
    private static readonly TimeSpan NotFoundRetryAfterFirstAttempt = TimeSpan.FromDays(7);
    private static readonly TimeSpan NotFoundRetryAfterSecondAttempt = TimeSpan.FromDays(30);
    private static readonly TimeSpan CoverRetryAfter = TimeSpan.FromDays(30);

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

        var candidates = await dbContext.Books
            .AsNoTracking()
            .Where(book =>
                (book.MetadataStatus == BookMetadataStatus.Pending &&
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
                  book.CoverCheckedAt <= now.Subtract(CoverRetryAfter))))
            .OrderBy(book => book.LastAttemptAt)
            .ThenBy(book => book.Id)
            .Take(command.BatchSize)
            .Select(book => book.Id)
            .ToListAsync(cancellationToken);

        var resolvedCount = 0;
        var notFoundCount = 0;
        var failedCount = 0;
        var coverUpdatedCount = 0;
        foreach (var isbn13 in candidates)
        {
            BookMetadataResult? metadata;
            try
            {
                metadata = await metadataResolver.ResolveAsync(isbn13, cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch
            {
                failedCount++;
                await RecordProviderFailureAsync(isbn13, now, cancellationToken);
                continue;
            }

            var book = await dbContext.Books
                .SingleOrDefaultAsync(candidate => candidate.Id == isbn13, cancellationToken);
            if (book is null)
            {
                continue;
            }

            var coverOnly = book.MetadataStatus == BookMetadataStatus.Resolved &&
                            book.CoverUrl is null;
            if (!coverOnly &&
                book.MetadataStatus is BookMetadataStatus.Manual or BookMetadataStatus.Resolved)
            {
                continue;
            }

            if (metadata is null)
            {
                if (coverOnly)
                {
                    book.RecordCoverCheck(now);
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
                if (coverOnly)
                {
                    book.RecordCoverCheck(now);
                }
                else
                {
                    book.RecordMetadataProviderFailure(now);
                }

                await dbContext.SaveChangesAsync(cancellationToken);
                continue;
            }

            var patch = CreatePatch(metadata, coverOnly);
            if (patch.Fields.Count == 0)
            {
                if (coverOnly)
                {
                    book.RecordCoverCheck(now);
                    await dbContext.SaveChangesAsync(cancellationToken);
                    continue;
                }

                failedCount++;
                book.RecordMetadataProviderFailure(now);
                await dbContext.SaveChangesAsync(cancellationToken);
                continue;
            }

            BookCoverSource? coverSource = null;
            var mappedCoverSource = default(BookCoverSource);
            if (metadata.CoverUrl is not null &&
                !TryMapCoverSource(metadata.CoverSource, metadata.Source, out mappedCoverSource))
            {
                failedCount++;
                if (coverOnly)
                {
                    book.RecordCoverCheck(now);
                }
                else
                {
                    book.RecordMetadataProviderFailure(now);
                }

                await dbContext.SaveChangesAsync(cancellationToken);
                continue;
            }
            coverSource = metadata.CoverUrl is null ? null : mappedCoverSource;

            try
            {
                book.ApplyAutomaticMetadata(
                    patch,
                    source,
                    metadata.RetrievedAt.UtcDateTime,
                    rawPayload: null,
                    coverSource: metadata.CoverUrl is null ? null : coverSource,
                    coverCheckedAt: metadata.RetrievedAt.UtcDateTime);
            }
            catch (ArgumentException)
            {
                failedCount++;
                if (coverOnly)
                {
                    book.RecordCoverCheck(now);
                }
                else
                {
                    book.RecordMetadataProviderFailure(now);
                }

                await dbContext.SaveChangesAsync(cancellationToken);
                continue;
            }

            if (metadata.CoverUrl is not null)
            {
                coverUpdatedCount++;
            }

            if (!coverOnly)
            {
                resolvedCount++;
            }

            await dbContext.SaveChangesAsync(cancellationToken);
        }

        return new EnrichPendingBooksResult(
            candidates.Count,
            resolvedCount,
            notFoundCount,
            failedCount,
            coverUpdatedCount);
    }

    private async Task RecordProviderFailureAsync(
        Isbn13 isbn13,
        DateTime attemptedAt,
        CancellationToken cancellationToken)
    {
        var book = await dbContext.Books
            .SingleOrDefaultAsync(candidate => candidate.Id == isbn13, cancellationToken);
        if (book is null)
        {
            return;
        }

        if (book.MetadataStatus == BookMetadataStatus.Resolved && book.CoverUrl is null)
        {
            if (book.RecordCoverCheck(attemptedAt))
            {
                await dbContext.SaveChangesAsync(cancellationToken);
            }

            return;
        }

        if (book.MetadataStatus == BookMetadataStatus.Manual)
        {
            return;
        }

        if (book.RecordMetadataProviderFailure(attemptedAt))
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
    }

    private static BookMetadataPatch CreatePatch(
        BookMetadataResult metadata,
        bool coverOnly)
    {
        var title = coverOnly ? null : Clean(metadata.Title);
        var authors = coverOnly ? null : Clean(metadata.Authors);
        var publisher = coverOnly ? null : Clean(metadata.Publisher);
        var workId = coverOnly ? null : Clean(metadata.WorkId);
        var fields = new List<BookMetadataField>();

        if (title is not null) fields.Add(BookMetadataField.Title);
        if (authors is not null) fields.Add(BookMetadataField.Authors);
        if (publisher is not null) fields.Add(BookMetadataField.Publisher);
        if (!coverOnly && metadata.PublicationYear is not null) fields.Add(BookMetadataField.PublicationYear);
        if (workId is not null) fields.Add(BookMetadataField.WorkId);
        if (metadata.CoverUrl is not null) fields.Add(BookMetadataField.CoverUrl);

        return new BookMetadataPatch(
            title,
            authors,
            publisher,
            coverOnly ? null : metadata.PublicationYear,
            PhysicalFormat: null,
            Language: null,
            Genre: null,
            CoverUrl: metadata.CoverUrl?.ToString(),
            fields,
            workId);
    }

    private static string? Clean(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
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
