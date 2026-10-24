using System.Text.Json;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Vole_Papillon_Damour.Application.Isbndb;
using Vole_Papillon_Damour.Application.Common.Interfaces.Persistence;
using Vole_Papillon_Damour.Application.Common.Interfaces.Services;
using Vole_Papillon_Damour.Application.Books.Common;
using Vole_Papillon_Damour.Application.Recommendations.Similarity;
using Vole_Papillon_Damour.Domain.BookAggregate;
using Vole_Papillon_Damour.Domain.BookAggregate.Entities;
using Vole_Papillon_Damour.Domain.BookAggregate.ValueObjects;
using Vole_Papillon_Damour.Domain.RecommendationAggregate;

namespace Vole_Papillon_Damour.Application.Recommendations.Commands.RefreshBookSimilarityProfiles;

public sealed class RefreshBookSimilarityProfilesCommandHandler(
    IProjectDbContext dbContext,
    IBibliographicNoticeReader noticeReader,
    IRecommendationSettings settings,
    IDateTimeProvider dateTimeProvider)
    : IRequestHandler<RefreshBookSimilarityProfilesCommand, RefreshBookSimilarityProfilesResult>
{
    private static readonly TimeSpan NoticeRefreshInterval = TimeSpan.FromDays(90);

    public async Task<RefreshBookSimilarityProfilesResult> Handle(
        RefreshBookSimilarityProfilesCommand request,
        CancellationToken cancellationToken)
    {
        if (!settings.Enabled)
        {
            return new RefreshBookSimilarityProfilesResult(0, 0, 0);
        }

        var now = dateTimeProvider.UtcNow;
        var refreshBefore = now - NoticeRefreshInterval;
        var bookQuery = dbContext.Books
            .AsNoTracking()
            .Where(book => !book.IsHiddenFromCatalog && book.RedirectedToIsbn13 == null);
        if (request.Isbn13 is { } requestedIsbn13)
        {
            bookQuery = bookQuery.Where(book => book.Id.Value == requestedIsbn13);
        }

        var books = await bookQuery
            .Select(book => new { Isbn13 = book.Id, book.UpdatedAt, book.IsIsbndbSourced })
            .ToListAsync(cancellationToken);
        var profileStatuses = await dbContext.BookSimilarityProfiles
            .AsNoTracking()
            .Select(profile => new { profile.Isbn13, profile.NoticeFetchedAt })
            .ToDictionaryAsync(profile => profile.Isbn13, cancellationToken);

        var candidates = books
            .Where(book =>
            {
                var isbn13 = book.Isbn13.Value;
                if (!profileStatuses.TryGetValue(isbn13, out var profile))
                {
                    return true;
                }

                return profile.NoticeFetchedAt is null ||
                       profile.NoticeFetchedAt < refreshBefore ||
                       profile.NoticeFetchedAt < book.UpdatedAt;
            })
            .OrderBy(book =>
                profileStatuses.TryGetValue(book.Isbn13.Value, out var profile)
                    ? profile.NoticeFetchedAt ?? DateTime.MinValue
                    : DateTime.MinValue)
            .ThenBy(book => book.UpdatedAt)
            .Take(Math.Max(0, settings.ProfileBatchSize))
            .ToArray();

        if (candidates.Length == 0)
        {
            return new RefreshBookSimilarityProfilesResult(0, 0, 0);
        }

        var isbndbCandidateIds = candidates
            .Where(candidate => candidate.IsIsbndbSourced)
            .Select(candidate => candidate.Isbn13)
            .ToArray();
        var isbndbBooks = isbndbCandidateIds.Length == 0
            ? new Dictionary<Isbn13, Book>()
            : await dbContext.Books
                .AsNoTracking()
                .Include(book => book.Genres)
                .Where(book => isbndbCandidateIds.Contains(book.Id))
                .ToDictionaryAsync(book => book.Id, cancellationToken);
        var isbndbRecords = isbndbCandidateIds.Length == 0
            ? new Dictionary<Isbn13, BookIsbndbRecord>()
            : await dbContext.BookIsbndbRecords
                .AsNoTracking()
                .Where(record => isbndbCandidateIds.Contains(record.Isbn13))
                .ToDictionaryAsync(record => record.Isbn13, cancellationToken);

        var candidateIsbns = candidates.Select(book => book.Isbn13.Value).ToArray();
        var profiles = await dbContext.BookSimilarityProfiles
            .Where(profile => candidateIsbns.Contains(profile.Isbn13))
            .ToDictionaryAsync(profile => profile.Isbn13, cancellationToken);

        var found = 0;
        var notFound = 0;
        foreach (var candidate in candidates)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var isbn13 = candidate.Isbn13.Value;
            var freeNotice = await noticeReader.ReadAsync(isbn13, cancellationToken);
            var edition = candidate.IsIsbndbSourced &&
                          isbndbBooks.TryGetValue(candidate.Isbn13, out var isbndbBook)
                ? CreateIsbndbEdition(
                    isbndbBook,
                    isbndbRecords.GetValueOrDefault(candidate.Isbn13),
                    freeNotice)
                : candidate.IsIsbndbSourced
                    ? null
                    : freeNotice;
            if (!profiles.TryGetValue(isbn13, out var profile))
            {
                profile = BookSimilarityProfile.Create(isbn13);
                profiles.Add(isbn13, profile);
                dbContext.BookSimilarityProfiles.Add(profile);
            }

            if (edition is null)
            {
                profile.RecordNotice(null, false, now);
                notFound++;
            }
            else
            {
                var book = await dbContext.Books
                    .SingleOrDefaultAsync(candidateBook => candidateBook.Id == candidate.Isbn13, cancellationToken);
                var editionSummary = BookSummaryText.Clean(edition.EditionSummary, edition.Title);
                var openLibrarySummary = BookSummaryText.Clean(edition.OpenLibraryDescription, edition.Title);
                var summary = editionSummary ?? openLibrarySummary;
                if (book is not null &&
                    !book.IsIsbndbSourced &&
                    summary is not null &&
                    book.Summary is null &&
                    (book.MetadataStatus is BookMetadataStatus.Resolved or BookMetadataStatus.Manual) &&
                    !book.IsMetadataFieldManuallyEdited(BookMetadataField.Summary))
                {
                    var summarySource = editionSummary is not null
                        ? BookMetadataSource.Bnf
                        : BookMetadataSource.OpenLibrary;
                    book.ApplyAutomaticMetadata(
                        new BookMetadataPatch(
                            null,
                            null,
                            null,
                            null,
                            null,
                            null,
                            null,
                            null,
                            [BookMetadataField.Summary],
                            Summary: summary),
                        summarySource,
                        now,
                        rawPayload: null);
                }

                var persistedEdition = edition with
                {
                    EditionSummary = null,
                    OpenLibraryDescription = candidate.IsIsbndbSourced
                        ? edition.OpenLibraryDescription
                        : null,
                };
                profile.RecordNotice(JsonSerializer.Serialize(persistedEdition), true, now);
                found++;
            }
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        return new RefreshBookSimilarityProfilesResult(candidates.Length, found, notFound);
    }

    private static SimilarityEdition CreateIsbndbEdition(
        Book book,
        BookIsbndbRecord? record,
        SimilarityEdition? freeNotice)
    {
        var title = Clean(book.Title) ?? Clean(record?.TitleLong) ?? string.Empty;
        var rawAuthors = ReadStringList(record?.Authors);
        var authors = SplitAuthors(book.Authors);
        var authorSurnames = rawAuthors
            .Select(GetIsbndbAuthorSurname)
            .Where(value => value is not null)
            .Select(value => value!)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        if (authorSurnames.Length == 0)
        {
            authorSurnames = authors
                .Select(GetLastName)
                .Where(value => value is not null)
                .Select(value => value!)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }

        var subjects = ReadStringList(record?.Subjects);
        var cleanSubjects = IsbndbGenreMapper.Map(
            subjects,
            record?.Publisher ?? book.Publisher,
            authors).UnmatchedSubjects;
        var forms = book.Genres
            .Select(genre => Clean(genre.Name))
            .Where(value => value is not null)
            .Select(value => value!)
            .Append(Clean(book.Genre))
            .Where(value => value is not null)
            .Select(value => value!)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var publisher = Clean(record?.Publisher) ?? Clean(book.Publisher);

        return new SimilarityEdition(
            book.Id.Value,
            title,
            freeNotice?.PartTitle,
            freeNotice?.PartNumber,
            GetSubtitle(title, record?.TitleLong),
            authors,
            authorSurnames,
            publisher,
            freeNotice?.Collection,
            freeNotice?.SeriesTitle,
            freeNotice?.SeriesNumber,
            freeNotice?.BnfWorkId,
            freeNotice?.OpenLibraryWorkKey ?? ToOpenLibraryWorkKey(book.WorkId),
            forms,
            cleanSubjects,
            freeNotice?.Audience333,
            freeNotice?.CnljReviewed == true,
            ReadFirstDewey(record?.DeweyDecimal),
            record?.Synopsis,
            freeNotice?.OpenLibraryDescription,
            freeNotice?.Languages);
    }

    private static IReadOnlyList<string> ReadStringList(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return [];
        }

        try
        {
            return JsonSerializer.Deserialize<string[]>(json) ?? [];
        }
        catch (JsonException)
        {
            return [];
        }
    }

    private static string? GetIsbndbAuthorSurname(string author)
    {
        var value = Clean(author);
        if (value is null)
        {
            return null;
        }

        var comma = value.IndexOf(',');
        return comma > 0
            ? Clean(value[..comma])
            : GetLastName(value);
    }

    private static string? GetLastName(string author)
    {
        var value = Clean(author);
        return value is null
            ? null
            : value.Split(' ', StringSplitOptions.RemoveEmptyEntries).LastOrDefault();
    }

    private static IReadOnlyList<string> SplitAuthors(string? authors) =>
        string.IsNullOrWhiteSpace(authors)
            ? []
            : authors.Split([',', ';'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();

    private static string? GetSubtitle(string title, string? titleLong)
    {
        var fullTitle = Clean(titleLong);
        if (fullTitle is null || fullTitle.Length <= title.Length)
        {
            return null;
        }

        if (fullTitle.StartsWith(title, StringComparison.OrdinalIgnoreCase))
        {
            var suffix = fullTitle[title.Length..].Trim(' ', ':', '-', '—', ',', ';', '.');
            return suffix.Length == 0 ? fullTitle : suffix;
        }

        return fullTitle;
    }

    private static string? ToOpenLibraryWorkKey(string? workId)
    {
        var value = Clean(workId);
        if (value is null)
        {
            return null;
        }

        return value.StartsWith("/works/", StringComparison.OrdinalIgnoreCase)
            ? value
            : value.StartsWith("OL", StringComparison.OrdinalIgnoreCase)
                ? $"/works/{value}"
                : null;
    }

    private static string? ReadFirstDewey(string? value)
    {
        var cleaned = Clean(value);
        if (cleaned is null)
        {
            return null;
        }

        try
        {
            using var document = JsonDocument.Parse(cleaned);
            var root = document.RootElement;
            if (root.ValueKind == JsonValueKind.Array)
            {
                root = root.EnumerateArray().FirstOrDefault();
            }

            if (root.ValueKind == JsonValueKind.String)
            {
                return Clean(root.GetString());
            }

            if (root.ValueKind == JsonValueKind.Number)
            {
                return root.GetRawText();
            }
        }
        catch (JsonException)
        {
            // ISBNdb stores this field as either a scalar or a JSON array.
        }

        return cleaned.Split([',', ';'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .FirstOrDefault();
    }

    private static string? Clean(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
