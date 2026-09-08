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
            .Where(book => !book.IsHiddenFromCatalog && book.RedirectedToIsbn13 == null);
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
                    EF.Functions.Like(
                        (book.Title ?? string.Empty)
                            .Replace("À", "a")
                            .Replace("Á", "a")
                            .Replace("Â", "a")
                            .Replace("Ä", "a")
                            .Replace("Ç", "c")
                            .Replace("È", "e")
                            .Replace("É", "e")
                            .Replace("Ê", "e")
                            .Replace("Ë", "e")
                            .Replace("Î", "i")
                            .Replace("Ï", "i")
                            .Replace("Ô", "o")
                            .Replace("Ö", "o")
                            .Replace("Ù", "u")
                            .Replace("Û", "u")
                            .Replace("Ü", "u")
                            .Replace("Ÿ", "y")
                            .ToLower()
                            .Replace("à", "a")
                            .Replace("á", "a")
                            .Replace("â", "a")
                            .Replace("ä", "a")
                            .Replace("ç", "c")
                            .Replace("è", "e")
                            .Replace("é", "e")
                            .Replace("ê", "e")
                            .Replace("ë", "e")
                            .Replace("î", "i")
                            .Replace("ï", "i")
                            .Replace("ô", "o")
                            .Replace("ö", "o")
                            .Replace("ù", "u")
                            .Replace("û", "u")
                            .Replace("ü", "u")
                            .Replace("ÿ", "y")
                            .Replace("œ", "oe"),
                        pattern) ||
                    EF.Functions.Like(
                        (book.Authors ?? string.Empty)
                            .Replace("À", "a")
                            .Replace("Á", "a")
                            .Replace("Â", "a")
                            .Replace("Ä", "a")
                            .Replace("Ç", "c")
                            .Replace("È", "e")
                            .Replace("É", "e")
                            .Replace("Ê", "e")
                            .Replace("Ë", "e")
                            .Replace("Î", "i")
                            .Replace("Ï", "i")
                            .Replace("Ô", "o")
                            .Replace("Ö", "o")
                            .Replace("Ù", "u")
                            .Replace("Û", "u")
                            .Replace("Ü", "u")
                            .Replace("Ÿ", "y")
                            .ToLower()
                            .Replace("à", "a")
                            .Replace("á", "a")
                            .Replace("â", "a")
                            .Replace("ä", "a")
                            .Replace("ç", "c")
                            .Replace("è", "e")
                            .Replace("é", "e")
                            .Replace("ê", "e")
                            .Replace("ë", "e")
                            .Replace("î", "i")
                            .Replace("ï", "i")
                            .Replace("ô", "o")
                            .Replace("ö", "o")
                            .Replace("ù", "u")
                            .Replace("û", "u")
                            .Replace("ü", "u")
                            .Replace("ÿ", "y")
                            .Replace("œ", "oe"),
                        pattern) ||
                    EF.Functions.Like(
                        (book.Publisher ?? string.Empty)
                            .Replace("À", "a")
                            .Replace("Á", "a")
                            .Replace("Â", "a")
                            .Replace("Ä", "a")
                            .Replace("Ç", "c")
                            .Replace("È", "e")
                            .Replace("É", "e")
                            .Replace("Ê", "e")
                            .Replace("Ë", "e")
                            .Replace("Î", "i")
                            .Replace("Ï", "i")
                            .Replace("Ô", "o")
                            .Replace("Ö", "o")
                            .Replace("Ù", "u")
                            .Replace("Û", "u")
                            .Replace("Ü", "u")
                            .Replace("Ÿ", "y")
                            .ToLower()
                            .Replace("à", "a")
                            .Replace("á", "a")
                            .Replace("â", "a")
                            .Replace("ä", "a")
                            .Replace("ç", "c")
                            .Replace("è", "e")
                            .Replace("é", "e")
                            .Replace("ê", "e")
                            .Replace("ë", "e")
                            .Replace("î", "i")
                            .Replace("ï", "i")
                            .Replace("ô", "o")
                            .Replace("ö", "o")
                            .Replace("ù", "u")
                            .Replace("û", "u")
                            .Replace("ü", "u")
                            .Replace("ÿ", "y")
                            .Replace("œ", "oe"),
                        pattern));
            }
        }

        if (!string.IsNullOrWhiteSpace(query.Genre))
        {
            var genre = query.Genre.Trim();
            filteredBooksQuery = filteredBooksQuery.Where(book =>
                EF.Functions.Like(book.Genre ?? string.Empty, genre));
        }

        if (query.Availability == PublicCatalogAvailabilityFilter.AvailableNow)
        {
            filteredBooksQuery = filteredBooksQuery.Where(book => book.QuantityAvailable > 0);
        }
        else if (query.Availability == PublicCatalogAvailabilityFilter.NextBookFair)
        {
            // Keep the availability predicate in SQL while passing only scalar
            // identifiers between the two queries. Value-object FK comparisons
            // cannot be translated reliably when correlated across providers.
            var bookFairIds = await dbContext.AssoEvents
                .AsNoTracking()
                .Where(assoEvent =>
                    !assoEvent.IsCancelled &&
                    assoEvent.EventsType == new EventsType(EventsType.EventsTypeEnum.Books))
                .Select(assoEvent => assoEvent.Id)
                .ToArrayAsync(cancellationToken);

            var nextFairIsbns = await dbContext.BookAnnouncements
                .AsNoTracking()
                .Where(announcement =>
                    announcement.Status == BookAnnouncementStatus.Announced &&
                    (announcement.AssoEventsId == null ||
                     bookFairIds.Contains(announcement.AssoEventsId!)))
                .Select(announcement => announcement.Isbn13)
                .Distinct()
                .ToArrayAsync(cancellationToken);

            filteredBooksQuery = filteredBooksQuery.Where(book =>
                nextFairIsbns.Contains(book.Id));
        }

        if (query.RareOnly)
        {
            filteredBooksQuery = filteredBooksQuery.Where(book => book.IsRare);
        }

        var genres = (await catalogBooksQuery
                .Where(book => book.Genre != null && book.Genre != string.Empty)
                .Select(book => book.Genre!)
                .Distinct()
                .OrderBy(genre => genre)
                .ToListAsync(cancellationToken))
            .Select(genre => genre.Trim())
            .Where(genre => !string.IsNullOrWhiteSpace(genre))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(genre => genre, StringComparer.CurrentCultureIgnoreCase)
            .ToArray();

        var totalCount = await filteredBooksQuery.CountAsync(cancellationToken);
        var orderedBooksQuery = OrderBooks(filteredBooksQuery, query.Sort, normalizedSearch);
        var pageBooks = await orderedBooksQuery
            .Skip(checked((query.Page - 1) * query.PageSize))
            .Take(query.PageSize)
            .ToListAsync(cancellationToken);

        var pageIsbns = pageBooks.Select(book => book.Id).ToArray();
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

        var page = PublicCatalogProjector.Project(pageBooks, announcements, fairs, nowUtc);

        return new PublicCatalogSearchResult(
            nowUtc,
            page,
            totalCount,
            query.Page,
            query.PageSize,
            genres);
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
