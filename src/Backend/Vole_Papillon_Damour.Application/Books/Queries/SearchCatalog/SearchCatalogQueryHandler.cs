using System.Globalization;
using System.Text;
using ErrorOr;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Hybrid;
using Vole_Papillon_Damour.Application.Books.Common;
using Vole_Papillon_Damour.Application.Common.Interfaces.Persistence;
using Vole_Papillon_Damour.Application.Common.Interfaces.Services;
using Vole_Papillon_Damour.Domain.BookAggregate;
using Vole_Papillon_Damour.Domain.BookAggregate.Entities;
using Vole_Papillon_Damour.Domain.BookAggregate.ValueObjects;
using Vole_Papillon_Damour.Domain.EventsAggregate.ValueObjects;
using Vole_Papillon_Damour.Domain.RareBookAggregate.ValueObjects;

namespace Vole_Papillon_Damour.Application.Books.Queries.SearchCatalog;

public sealed class SearchCatalogQueryHandler(
    IProjectDbContext dbContext,
    IDateTimeProvider dateTimeProvider,
    HybridCache cache)
    : IRequestHandler<SearchCatalogQuery, ErrorOr<PublicCatalogSearchResult>>
{
    private const int MaxPageSize = 60;

    public async Task<ErrorOr<PublicCatalogSearchResult>> Handle(
        SearchCatalogQuery query,
        CancellationToken cancellationToken)
    {
        if (query.Page <= 0 || query.PageSize <= 0 || query.PageSize > MaxPageSize)
        {
            return Error.Validation(
                "Catalog.InvalidPaging",
                $"Page must be positive and page size must be between 1 and {MaxPageSize}.");
        }

        var nowUtc = dateTimeProvider.UtcNow;
        if (nowUtc.Kind != DateTimeKind.Utc)
        {
            return Error.Validation(
                "Catalog.InvalidClock",
                "The catalog clock must be expressed in UTC.");
        }

        var inProgressScanIsbns = await PublicCatalogBookVisibility.GetInProgressScanIsbnsAsync(
            dbContext,
            cancellationToken);
        var catalogBooksQuery = PublicCatalogBookVisibility.FilterPublicCatalogBooks(
            dbContext.Books.AsNoTracking(),
            inProgressScanIsbns);
        var genreFacets = await PublicCatalogGenreFacets.GetAsync(
            catalogBooksQuery,
            cache,
            cancellationToken);
        var genres = genreFacets.Select(facet => facet.Name).ToArray();
        var normalizedSearch = Normalize(query.Search);
        var searchTerms = query.Search?
            .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(Normalize)
            .Where(term => !string.IsNullOrWhiteSpace(term))
            .ToArray()
            ?? [];

        // SQL Server's accent-insensitive collation and LIKE keep the searchable
        // fields in the database. There is deliberately no fallback that loads
        // the complete Books table when a search has no SQL candidates.
        var filteredBooksQuery = catalogBooksQuery;
        var trimmedSearch = query.Search?.Trim();
        if (trimmedSearch is { Length: 13 } &&
            trimmedSearch.All(char.IsAsciiDigit) &&
            Isbn13.TryCreate(trimmedSearch, out var canonicalIsbn))
        {
            filteredBooksQuery = filteredBooksQuery.Where(book => book.Id == canonicalIsbn);
        }
        else
        {
            foreach (var term in searchTerms)
            {
                var pattern = $"%{term}%";
                filteredBooksQuery = filteredBooksQuery.Where(book =>
                    EF.Functions.Like(book.Title, pattern) ||
                    EF.Functions.Like(book.Authors, pattern) ||
                    EF.Functions.Like(book.Publisher, pattern));
            }
        }
        if (!string.IsNullOrWhiteSpace(query.Genre))
        {
            var genre = PublicCatalogGenreFacets.ResolveCanonicalName(query.Genre, genreFacets);
            if (genre is null)
            {
                return new PublicCatalogSearchResult(
                    nowUtc,
                    [],
                    0,
                    HasMore: false,
                    query.Page,
                    query.PageSize,
                    genres,
                    genreFacets);
            }

            filteredBooksQuery = filteredBooksQuery.Where(book =>
                book.Genres.Any(bookGenre => bookGenre.Name == EF.Constant(genre)));
        }

        // Active announcements are a correlated subquery in the book predicates, so the
        // ISBN list is never materialized. Only the (small) list of book fair ids is loaded.
        IQueryable<BookAnnouncement>? activeAnnouncements = null;
        var needsActiveAnnouncements =
            query.Availability == PublicCatalogAvailabilityFilter.NextBookFair ||
            (query.Availability == PublicCatalogAvailabilityFilter.All && !query.IncludeExhausted) ||
            (query.Availability == PublicCatalogAvailabilityFilter.AvailableNow && query.IncludeExhausted);
        if (needsActiveAnnouncements)
        {
            var bookFairIds = await dbContext.AssoEvents
                .AsNoTracking()
                .Where(assoEvent =>
                    !assoEvent.IsCancelled &&
                    assoEvent.EventsType == new EventsType(EventsType.EventsTypeEnum.Books))
                .Select(assoEvent => assoEvent.Id)
                .ToArrayAsync(cancellationToken);

            activeAnnouncements = dbContext.BookAnnouncements
                .AsNoTracking()
                .Where(announcement =>
                    announcement.Status == BookAnnouncementStatus.Announced &&
                    (announcement.AssoEventsId == null ||
                     bookFairIds.Contains(announcement.AssoEventsId!)));
        }

        if (query.Availability == PublicCatalogAvailabilityFilter.AvailableNow)
        {
            filteredBooksQuery = query.IncludeExhausted
                ? filteredBooksQuery.Where(book =>
                    book.QuantityAvailable > 0 ||
                    (book.QuantityAvailable <= 0 &&
                     !activeAnnouncements!.Any(announcement => announcement.Isbn13 == book.Id)))
                : filteredBooksQuery.Where(book => book.QuantityAvailable > 0);
        }
        else if (query.Availability == PublicCatalogAvailabilityFilter.NextBookFair)
        {
            filteredBooksQuery = query.IncludeExhausted
                ? filteredBooksQuery.Where(book =>
                    activeAnnouncements!.Any(announcement => announcement.Isbn13 == book.Id) ||
                    (book.QuantityAvailable <= 0 &&
                     !activeAnnouncements!.Any(announcement => announcement.Isbn13 == book.Id)))
                : filteredBooksQuery.Where(book =>
                    activeAnnouncements!.Any(announcement => announcement.Isbn13 == book.Id));
        }
        else if (!query.IncludeExhausted)
        {
            filteredBooksQuery = filteredBooksQuery.Where(book =>
                book.QuantityAvailable > 0 ||
                activeAnnouncements!.Any(announcement => announcement.Isbn13 == book.Id));
        }

        if (query.RareOnly)
        {
            filteredBooksQuery = filteredBooksQuery.Where(book => dbContext.RareBooks
                .AsNoTracking()
                .PublishedAvailable()
                .Any(rareBook => rareBook.Isbn13 == book.Id));
        }

        var orderedBooksQuery = OrderBooks(filteredBooksQuery, query.Sort, normalizedSearch);
        var pageOffset = checked((query.Page - 1) * query.PageSize);
        var pageIsbnsWithLookahead = await orderedBooksQuery
            .Select(book => book.Id)
            .Skip(pageOffset)
            .Take(query.PageSize + 1)
            .ToListAsync(cancellationToken);
        var hasMore = pageIsbnsWithLookahead.Count > query.PageSize;
        if (hasMore)
        {
            pageIsbnsWithLookahead.RemoveAt(pageIsbnsWithLookahead.Count - 1);
        }

        var pageIsbns = pageIsbnsWithLookahead.ToArray();
        var projectedPageBooks = pageIsbns.Length == 0
            ? []
            : await dbContext.Books
                .AsNoTracking()
                .Where(book => pageIsbns.Contains(book.Id))
                .Select(book => new PublicCatalogBookProjection(
                    book.Id.Value,
                    book.Title,
                    book.Authors,
                    book.Publisher,
                    book.PublicationYear,
                    book.PhysicalFormat,
                    book.Language,
                    book.Genre,
                    book.WorkId,
                    book.CoverUrl,
                    book.QuantityAvailable,
                    book.LastAvailableAt,
                    book.FirstSeenAt,
                    book.UpdatedAt,
                    book.MetadataStatus,
                    book.IsHiddenFromCatalog,
                    book.IsFavoriteReference,
                    book.RedirectedToIsbn13 != null,
                    book.CoverSource,
                    book.Genres.Select(genre => genre.Name).ToArray()))
                .ToListAsync(cancellationToken);
        var projectedBooksByIsbn = projectedPageBooks.ToDictionary(
            book => book.Isbn13,
            StringComparer.Ordinal);
        var photoCovers = await BookPhotoCovers.FirstPhotoUrlsAsync(
            dbContext,
            pageIsbns.Where(isbn => isbn.IsInternal).ToArray(),
            cancellationToken);
        var pageBooks = pageIsbns
            .Select(isbn => projectedBooksByIsbn[isbn.Value] is { CoverUrl: null } uncovered &&
                            photoCovers.TryGetValue(isbn.Value, out var photoUrl)
                ? uncovered with { CoverUrl = photoUrl }
                : projectedBooksByIsbn[isbn.Value])
            .ToArray();

        int? totalCount;
        if (query.IncludeTotalCount)
        {
            totalCount = await filteredBooksQuery.CountAsync(cancellationToken);
        }
        else
        {
            totalCount = !hasMore && (pageBooks.Length > 0 || query.Page == 1)
                ? checked(pageOffset + pageBooks.Length)
                : null;
        }

        var publishedAvailableRareIsbns = pageIsbns.Length == 0
            ? []
            : (await dbContext.RareBooks
                .AsNoTracking()
                .PublishedAvailable()
                .Where(rareBook => pageIsbns.Contains(rareBook.Isbn13!.Value))
                .Select(rareBook => rareBook.Isbn13!.Value.Value)
                .ToListAsync(cancellationToken))
                .ToHashSet(StringComparer.Ordinal);
        var announcements = pageIsbns.Length == 0
            ? []
            : await dbContext.BookAnnouncements
                .AsNoTracking()
                .Where(announcement => pageIsbns.Contains(announcement.Isbn13))
                .ToListAsync(cancellationToken);
        var fairIds = announcements
            .Where(announcement => announcement.AssoEventsId is not null)
            .Select(announcement => announcement.AssoEventsId!)
            .Distinct()
            .ToArray();
        var fairs = fairIds.Length == 0
            ? []
            : await dbContext.AssoEvents
                .AsNoTracking()
                .Where(assoEvent => fairIds.Contains(assoEvent.Id))
                .ToListAsync(cancellationToken);

        var page = PublicCatalogProjector.Project(
            pageBooks,
            announcements,
            fairs,
            publishedAvailableRareIsbns,
            nowUtc);

        return new PublicCatalogSearchResult(
            nowUtc,
            page,
            totalCount,
            hasMore,
            query.Page,
            query.PageSize,
            genres,
            genreFacets);
    }

    private static IOrderedQueryable<Book> OrderBooks(
        IQueryable<Book> books,
        PublicCatalogSortOrder sort,
        string normalizedSearch)
    {
        if (sort == PublicCatalogSortOrder.RecentlyAdded)
        {
            return books
                .OrderByDescending(book => book.FirstSeenAt)
                .ThenBy(book => book.Title)
                .ThenBy(book => book.Id);
        }

        if (string.IsNullOrWhiteSpace(normalizedSearch))
        {
            return books
                .OrderByDescending(book => book.UpdatedAt)
                .ThenBy(book => book.Title)
                .ThenBy(book => book.Id);
        }

        var exactPattern = normalizedSearch;
        var startsWithPattern = $"{normalizedSearch}%";
        var containsPattern = $"%{normalizedSearch}%";
        return books
            .OrderByDescending(book => EF.Functions.Like(book.Title ?? string.Empty, exactPattern))
            .ThenByDescending(book => EF.Functions.Like(book.Title ?? string.Empty, startsWithPattern))
            .ThenByDescending(book => EF.Functions.Like(book.Title ?? string.Empty, containsPattern))
            .ThenByDescending(book => EF.Functions.Like(book.Authors ?? string.Empty, containsPattern))
            .ThenByDescending(book => book.UpdatedAt)
            .ThenBy(book => book.Title)
            .ThenBy(book => book.Id);
    }

    private static string Normalize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var decomposed = value.Trim().Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(decomposed.Length);
        foreach (var character in decomposed)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(character) != UnicodeCategory.NonSpacingMark)
            {
                builder.Append(char.ToLowerInvariant(character));
            }
        }

        return builder.ToString().Normalize(NormalizationForm.FormC);
    }
}
