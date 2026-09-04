using System.Globalization;
using System.Text;
using ErrorOr;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Vole_Papillon_Damour.Application.Books.Common;
using Vole_Papillon_Damour.Application.Common.Interfaces.Persistence;
using Vole_Papillon_Damour.Application.Common.Interfaces.Services;
using Vole_Papillon_Damour.Domain.BookAggregate;

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

        var booksQuery = dbContext.Books
            .AsNoTracking()
            .Where(book => !book.IsHiddenFromCatalog);
        var normalizedSearch = Normalize(query.Search);
        var searchTerms = query.Search?
            .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            ?? [];

        // SQL Server's accent-insensitive collation and LIKE narrow the hot path
        // before the application applies the same normalization to every field.
        // The fallback keeps the contract deterministic on providers used for
        // tests (and for ISBNs containing separators not stored in the database).
        List<Book> books;
        if (searchTerms.Length > 0)
        {
            var sqlCandidates = booksQuery;
            foreach (var term in searchTerms)
            {
                var pattern = $"%{term}%";
                sqlCandidates = sqlCandidates.Where(book =>
                    EF.Functions.Like(book.Title ?? string.Empty, pattern) ||
                    EF.Functions.Like(book.Authors ?? string.Empty, pattern) ||
                    EF.Functions.Like(book.Publisher ?? string.Empty, pattern));
            }

            books = await sqlCandidates.ToListAsync(cancellationToken);
            if (books.Count == 0)
            {
                books = await booksQuery.ToListAsync(cancellationToken);
            }
        }
        else
        {
            books = await booksQuery.ToListAsync(cancellationToken);
        }

        var announcements = await dbContext.BookAnnouncements
            .AsNoTracking()
            .ToListAsync(cancellationToken);
        var fairs = await dbContext.AssoEvents
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        var projected = PublicCatalogProjector.Project(books, announcements, fairs, nowUtc);
        var genres = projected
            .Select(book => book.Genre?.Trim())
            .Where(genre => !string.IsNullOrWhiteSpace(genre))
            .Select(genre => genre!)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(genre => genre, StringComparer.CurrentCultureIgnoreCase)
            .ToArray()!;

        var filtered = projected.AsEnumerable();
        if (!string.IsNullOrWhiteSpace(normalizedSearch))
        {
            var terms = normalizedSearch
                .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            filtered = filtered.Where(book => terms.All(term => Matches(book, term)));
        }

        if (!string.IsNullOrWhiteSpace(query.Genre))
        {
            var requestedGenre = Normalize(query.Genre);
            filtered = filtered.Where(book => Normalize(book.Genre) == requestedGenre);
        }

        filtered = query.Availability switch
        {
            PublicCatalogAvailabilityFilter.AvailableNow =>
                filtered.Where(book => book.QuantityAvailable > 0),
            PublicCatalogAvailabilityFilter.NextBookFair =>
                filtered.Where(book => book.QuantityAnnounced > 0),
            _ => filtered
        };

        if (query.RareOnly)
        {
            filtered = filtered.Where(book => book.IsRare);
        }

        var ordered = query.Sort switch
        {
            PublicCatalogSortOrder.RecentlyAdded =>
                filtered.OrderByDescending(book => book.FirstSeenAt)
                    .ThenBy(book => book.Title, StringComparer.CurrentCultureIgnoreCase),
            _ => OrderByRelevance(filtered, normalizedSearch)
        };

        var materialized = ordered.ToArray();
        var page = materialized
            .Skip(checked((query.Page - 1) * query.PageSize))
            .Take(query.PageSize)
            .ToArray();

        return new PublicCatalogSearchResult(
            nowUtc,
            page,
            materialized.Length,
            query.Page,
            query.PageSize,
            genres);
    }

    private static IOrderedEnumerable<PublicCatalogBookResult> OrderByRelevance(
        IEnumerable<PublicCatalogBookResult> books,
        string normalizedSearch)
    {
        return books
            .OrderByDescending(book => Score(book, normalizedSearch))
            .ThenByDescending(book => book.UpdatedAt)
            .ThenBy(book => book.Title, StringComparer.CurrentCultureIgnoreCase);
    }

    private static int Score(PublicCatalogBookResult book, string normalizedSearch)
    {
        if (string.IsNullOrWhiteSpace(normalizedSearch))
        {
            return 0;
        }

        var title = Normalize(book.Title);
        var authors = Normalize(book.Authors);
        var isbn = Normalize(book.Isbn13);

        if (isbn == normalizedSearch || title == normalizedSearch)
        {
            return 100;
        }

        if (title.StartsWith(normalizedSearch, StringComparison.Ordinal))
        {
            return 80;
        }

        if (title.Contains(normalizedSearch, StringComparison.Ordinal))
        {
            return 60;
        }

        if (authors.Contains(normalizedSearch, StringComparison.Ordinal))
        {
            return 40;
        }

        return isbn.Contains(normalizedSearch, StringComparison.Ordinal) ? 30 : 0;
    }

    private static bool Matches(PublicCatalogBookResult book, string term)
    {
        return Normalize(book.Title).Contains(term, StringComparison.Ordinal) ||
               Normalize(book.Authors).Contains(term, StringComparison.Ordinal) ||
               Normalize(book.Publisher).Contains(term, StringComparison.Ordinal) ||
               Normalize(book.Isbn13).Contains(term, StringComparison.Ordinal);
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
