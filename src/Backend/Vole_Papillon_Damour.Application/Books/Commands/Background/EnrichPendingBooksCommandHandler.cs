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
    IDateTimeProvider dateTimeProvider,
    IBookCoverStorage? coverStorage = null)
    : IRequestHandler<EnrichPendingBooksCommand, EnrichPendingBooksResult>
{
    private static readonly TimeSpan NotFoundRetryAfterFirstAttempt = TimeSpan.FromDays(7);
    private static readonly TimeSpan NotFoundRetryAfterSecondAttempt = TimeSpan.FromDays(30);

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
                book.MetadataStatus == BookMetadataStatus.Pending ||
                (book.MetadataStatus == BookMetadataStatus.NotFound &&
                 ((book.ResolveAttempts == 1 &&
                   book.LastAttemptAt <= now.Subtract(NotFoundRetryAfterFirstAttempt)) ||
                  (book.ResolveAttempts == 2 &&
                   book.LastAttemptAt <= now.Subtract(NotFoundRetryAfterSecondAttempt)))))
            .OrderBy(book => book.LastAttemptAt)
            .ThenBy(book => book.Id)
            .Take(command.BatchSize)
            .Select(book => book.Id)
            .ToListAsync(cancellationToken);

        var resolvedCount = 0;
        var notFoundCount = 0;
        var failedCount = 0;
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
                continue;
            }

            var book = await dbContext.Books
                .SingleOrDefaultAsync(candidate => candidate.Id == isbn13, cancellationToken);
            if (book is null ||
                book.MetadataStatus is BookMetadataStatus.Manual or BookMetadataStatus.Resolved)
            {
                continue;
            }

            if (metadata is null)
            {
                book.RecordMetadataNotFound(now);
                notFoundCount++;
            }
            else
            {
                if (!TryMapSource(metadata.Source, out var source) ||
                    !string.Equals(metadata.Isbn13, isbn13.Value, StringComparison.Ordinal))
                {
                    failedCount++;
                    continue;
                }

                var patch = CreatePatch(metadata);
                if (coverStorage is not null && metadata.CoverUrl is not null)
                {
                    Uri? coverBlobRef;
                    try
                    {
                        coverBlobRef = await coverStorage.TryStoreAsync(
                            isbn13,
                            metadata.CoverUrl,
                            cancellationToken);
                    }
                    catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                    {
                        throw;
                    }
                    catch
                    {
                        failedCount++;
                        continue;
                    }

                    if (coverBlobRef is not null)
                    {
                        patch = patch with
                        {
                            CoverBlobRef = coverBlobRef.ToString(),
                            Fields = patch.Fields
                                .Append(BookMetadataField.CoverBlobRef)
                                .ToArray()
                        };
                    }
                }

                if (patch.Fields.Count == 0)
                {
                    failedCount++;
                    continue;
                }

                book.ApplyAutomaticMetadata(
                    patch,
                    source,
                    metadata.RetrievedAt.UtcDateTime,
                    rawPayload: null);
                resolvedCount++;
            }

            await dbContext.SaveChangesAsync(cancellationToken);
        }

        return new EnrichPendingBooksResult(
            candidates.Count,
            resolvedCount,
            notFoundCount,
            failedCount);
    }

    private static BookMetadataPatch CreatePatch(BookMetadataResult metadata)
    {
        var title = Clean(metadata.Title);
        var authors = Clean(metadata.Authors);
        var publisher = Clean(metadata.Publisher);
        var workId = Clean(metadata.WorkId);
        var fields = new List<BookMetadataField>();

        if (title is not null) fields.Add(BookMetadataField.Title);
        if (authors is not null) fields.Add(BookMetadataField.Authors);
        if (publisher is not null) fields.Add(BookMetadataField.Publisher);
        if (metadata.PublicationYear is not null) fields.Add(BookMetadataField.PublicationYear);
        if (workId is not null) fields.Add(BookMetadataField.WorkId);

        return new BookMetadataPatch(
            title,
            authors,
            publisher,
            metadata.PublicationYear,
            PhysicalFormat: null,
            Language: null,
            Genre: null,
            CoverBlobRef: null,
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

        mappedSource = default;
        return false;
    }
}
