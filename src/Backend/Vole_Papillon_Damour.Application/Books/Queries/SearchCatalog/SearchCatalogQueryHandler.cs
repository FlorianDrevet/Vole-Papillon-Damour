using System.Globalization;
using System.Text;
using ErrorOr;
using MediatR;
using Microsoft.EntityFrameworkCore;
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
    IDateTimeProvider dateTimeProvider)
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

        var catalogBooksQuery = dbContext.Books
            .AsNoTracking()
            .Where(PublicCatalogBookVisibility.Predicate);
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
            var genre = query.Genre.Trim();
            filteredBooksQuery = filteredBooksQuery.Where(book =>
                book.Genres.Any(bookGenre => bookGenre.Name == genre));
        }

        var activeAnnouncementIsbns = Array.Empty<Isbn13>();
        var needsActiveAnnouncementIsbns =
            query.Availability == PublicCatalogAvailabilityFilter.NextBookFair ||
            (query.Availability == PublicCatalogAvailabilityFilter.All && !query.IncludeExhausted) ||
            (query.Availability == PublicCatalogAvailabilityFilter.AvailableNow && query.IncludeExhausted);
        if (needsActiveAnnouncementIsbns)
        {
            // Keep the availability predicate in SQL while resolving announcement
            // identifiers separately. Correlating the value-object foreign keys
            // directly is not translated consistently across providers.
            var bookFairIds = await dbContext.AssoEvents
                .AsNoTracking()
                .Where(assoEvent =>
                    !assoEvent.IsCancelled &&
                    assoEvent.EventsType == new EventsType(EventsType.EventsTypeEnum.Books))
                .Select(assoEvent => assoEvent.Id)
                .ToArrayAsync(cancellationToken);

            activeAnnouncementIsbns = await dbContext.BookAnnouncements
                .AsNoTracking()
                .Where(announcement =>
                    announcement.Status == BookAnnouncementStatus.Announced &&
                    (announcement.AssoEventsId == null ||
                     bookFairIds.Contains(announcement.AssoEventsId!)))
                .Select(announcement => announcement.Isbn13)
                .Distinct()
                .ToArrayAsync(cancellationToken);
        }

        if (query.Availability == PublicCatalogAvailabilityFilter.AvailableNow)
        {
            filteredBooksQuery = query.IncludeExhausted
                ? filteredBooksQuery.Where(book =>
                    book.QuantityAvailable > 0 ||
                    (book.QuantityAvailable <= 0 && !activeAnnouncementIsbns.Contains(book.Id)))
                : filteredBooksQuery.Where(book => book.QuantityAvailable > 0);
        }
        else if (query.Availability == PublicCatalogAvailabilityFilter.NextBookFair)
        {
            filteredBooksQuery = query.IncludeExhausted
                ? filteredBooksQuery.Where(book =>
                    activeAnnouncementIsbns.Contains(book.Id) ||
                    (book.QuantityAvailable <= 0 && !activeAnnouncementIsbns.Contains(book.Id)))
                : filteredBooksQuery.Where(book => activeAnnouncementIsbns.Contains(book.Id));
        }
        else if (!query.IncludeExhausted)
        {
            filteredBooksQuery = filteredBooksQuery.Where(book =>
                book.QuantityAvailable > 0 || activeAnnouncementIsbns.Contains(book.Id));
        }

        if (query.RareOnly)
        {
            filteredBooksQuery = filteredBooksQuery.Where(book => dbContext.RareBooks
                .AsNoTracking()
                .PublishedAvailable()
                .Any(rareBook => rareBook.Isbn13 == book.Id));
        }

        var genreFacetRows = await catalogBooksQuery
            .SelectMany(book => book.Genres)
            .Where(bookGenre => bookGenre.Name != string.Empty)
            .GroupBy(bookGenre => bookGenre.Name)
            .Select(group => new PublicCatalogGenreFacetResult(group.Key, group.Count()))
            .ToListAsync(cancellationToken);
        var genreFacets = genreFacetRows
            .GroupBy(facet => facet.Name.Trim(), StringComparer.OrdinalIgnoreCase)
            .Select(group => new PublicCatalogGenreFacetResult(
                group.Key,
                group.Sum(facet => facet.BookCount)))
            .OrderByDescending(facet => facet.BookCount)
            .ThenBy(facet => facet.Name, StringComparer.CurrentCultureIgnoreCase)
            .ToArray();
        var genres = genreFacets.Select(facet => facet.Name).ToArray();

        var orderedBooksQuery = OrderBooks(filteredBooksQuery, query.Sort, normalizedSearch);
        var pageOffset = checked((query.Page - 1) * query.PageSize);
        var pageBooksWithLookahead = await orderedBooksQuery
            .Include(book => book.Genres)
            .Skip(pageOffset)
            .Take(query.PageSize + 1)
            .ToListAsync(cancellationToken);
        var hasMore = pageBooksWithLookahead.Count > query.PageSize;
        if (hasMore)
        {
            pageBooksWithLookahead.RemoveAt(pageBooksWithLookahead.Count - 1);
        }

        var pageBooks = pageBooksWithLookahead;
        int? totalCount = !hasMore && (pageBooks.Count > 0 || query.Page == 1)
            ? checked(pageOffset + pageBooks.Count)
            : null;

        var pageIsbns = pageBooks.Select(book => book.Id).ToArray();
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
