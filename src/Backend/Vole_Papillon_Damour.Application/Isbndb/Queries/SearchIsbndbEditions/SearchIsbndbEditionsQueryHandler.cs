using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using ErrorOr;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.Options;
using Vole_Papillon_Damour.Application.Books.Common;
using Vole_Papillon_Damour.Application.Common.Interfaces.Persistence;
using Vole_Papillon_Damour.Application.Common.Interfaces.Services;
using Vole_Papillon_Damour.Domain.BookAggregate.ValueObjects;
using Vole_Papillon_Damour.Domain.IsbndbAggregate;

namespace Vole_Papillon_Damour.Application.Isbndb.Queries.SearchIsbndbEditions;

public sealed class SearchIsbndbEditionsQueryHandler : IRequestHandler<SearchIsbndbEditionsQuery, ErrorOr<SearchIsbndbEditionsResult>>
{
    private static readonly Regex YearPrefix = new(@"\b(\d{4})", RegexOptions.Compiled | RegexOptions.CultureInvariant);
    private readonly IProjectDbContext _dbContext;
    private readonly IIsbndbClient _client;
    private readonly HybridCache _cache;
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly IsbndbPublicSearchOptions _options;
    private readonly HybridCacheEntryOptions _cacheOptions;

    public SearchIsbndbEditionsQueryHandler(
        IProjectDbContext dbContext,
        IIsbndbClient client,
        HybridCache cache,
        IDateTimeProvider dateTimeProvider,
        IOptions<IsbndbPublicSearchOptions> options)
    {
        _dbContext = dbContext;
        _client = client;
        _cache = cache;
        _dateTimeProvider = dateTimeProvider;
        _options = options.Value;
        var cacheDuration = TimeSpan.FromMinutes(Math.Max(1, _options.SearchCacheMinutes));
        _cacheOptions = new HybridCacheEntryOptions
        {
            Expiration = cacheDuration,
            LocalCacheExpiration = cacheDuration
        };
    }

    public async Task<ErrorOr<SearchIsbndbEditionsResult>> Handle(
        SearchIsbndbEditionsQuery query,
        CancellationToken cancellationToken)
    {
        var text = query.Query?.Trim() ?? string.Empty;
        if (text.Length is < 3 or > 200)
        {
            return Error.Validation(
                "Isbndb.SearchQueryInvalid",
                "Search text must contain between 3 and 200 characters after trimming.");
        }

        var normalizedKey = NormalizeQuery(text);
        try
        {
            return await _cache.GetOrCreateAsync(
                $"isbndb-search:{normalizedKey}",
                new SearchState(this, text),
                static (state, factoryCancellationToken) => state.Handler.SearchUncachedAsync(
                    state.Query,
                    factoryCancellationToken),
                _cacheOptions,
                cancellationToken: cancellationToken);
        }
        catch (UncacheableSearchException exception)
        {
            return exception.Result;
        }
    }

    private async ValueTask<SearchIsbndbEditionsResult> SearchUncachedAsync(
        string query,
        CancellationToken cancellationToken)
    {
        var now = _dateTimeProvider.UtcNow;
        if (now.Kind != DateTimeKind.Utc)
        {
            throw new InvalidOperationException("The public ISBNdb search clock must be expressed in UTC.");
        }

        var today = DateOnly.FromDateTime(now);
        var usage = await _dbContext.IsbndbDailyUsages.AsNoTracking()
            .SingleOrDefaultAsync(
                entry => entry.Day == today && entry.Usage == IsbndbUsage.PublicSearch,
                cancellationToken);
        if (usage?.Credits >= _options.PublicSearchDailyCredits)
        {
            throw new UncacheableSearchException(new SearchIsbndbEditionsResult([], 0, false, "limit"));
        }

        // HybridCache shares an in-flight factory. Its token is cancelled only after every
        // caller has cancelled, so one disconnected browser does not abort another visitor's search.
        var search = await _client.SearchAsync(query, IsbndbUsage.PublicSearch, cancellationToken);
        if (search.Status == IsbndbSearchStatus.QuotaExhausted)
        {
            throw new UncacheableSearchException(new SearchIsbndbEditionsResult([], 0, false, "quota"));
        }

        if (search.Status == IsbndbSearchStatus.Unavailable)
        {
            throw new UncacheableSearchException(new SearchIsbndbEditionsResult([], 0, false, "unavailable"));
        }

        var mapped = new List<(Isbn13 Isbn13, SearchIsbndbEditionResult Item)>();
        foreach (var book in search.Books)
        {
            if (!string.Equals(book.Language?.Trim(), "fra", StringComparison.OrdinalIgnoreCase) ||
                !book.TryGetIsbn13(out var isbn13))
            {
                continue;
            }

            mapped.Add((isbn13, Map(book, isbn13)));
        }

        var isbn13s = mapped.Select(item => item.Isbn13).Distinct().ToArray();
        var visibleIsbns = isbn13s.Length == 0
            ? new HashSet<Isbn13>()
            : (await PublicCatalogBookVisibility.FilterPublicCatalogBooks(
                    _dbContext.Books.AsNoTracking(),
                    _dbContext)
                .Where(book => isbn13s.Contains(book.Id))
                .Select(book => book.Id)
                .ToListAsync(cancellationToken))
                .ToHashSet();

        var items = mapped
            .Where(item => !visibleIsbns.Contains(item.Isbn13))
            .DistinctBy(item => item.Isbn13)
            .Select(item => item.Item)
            .ToArray();
        var total = Math.Max(0, search.Total);
        return new SearchIsbndbEditionsResult(items, total, total > 100, null);
    }

    private static SearchIsbndbEditionResult Map(IsbndbBook book, Isbn13 isbn13)
    {
        var title = FirstNonBlank(book.Title, book.TitleLong);
        var authors = book.Authors is null
            ? null
            : string.Join(", ", book.Authors
                .SelectMany(author => author.Split(';', StringSplitOptions.RemoveEmptyEntries))
                .Select(NormalizeAuthor)
                .Where(author => !string.IsNullOrWhiteSpace(author)));
        if (string.IsNullOrWhiteSpace(authors)) authors = null;

        var yearMatch = YearPrefix.Match(book.DatePublished ?? string.Empty);
        int? year = yearMatch.Success && int.TryParse(
            yearMatch.Groups[1].Value,
            NumberStyles.None,
            CultureInfo.InvariantCulture,
            out var parsedYear) && parsedYear is >= 1 and <= 9999
            ? parsedYear
            : null;
        var patch = IsbndbNoticeMapper.ToPatch(book);
        return new SearchIsbndbEditionResult(
            isbn13.Value,
            title,
            authors,
            FirstNonBlank(book.Publisher),
            year,
            ToCoverUrl(book.Image),
            patch.PhysicalFormat);
    }

    private static string NormalizeAuthor(string author)
    {
        var value = author.Trim();
        var comma = value.IndexOf(',');
        if (comma < 0) return value;
        var surname = value[..comma].Trim();
        var givenNames = value[(comma + 1)..].Trim();
        return surname.Length == 0 ? givenNames : givenNames.Length == 0 ? surname : $"{givenNames} {surname}";
    }

    private static string? ToCoverUrl(string? image) =>
        image?.Trim() is { Length: <= 2048 } value &&
        Uri.TryCreate(value, UriKind.Absolute, out var uri) &&
        uri.Scheme.Equals(Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase)
            ? value
            : null;

    private static string? FirstNonBlank(params string?[] values) =>
        values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value))?.Trim();

    private static string NormalizeQuery(string value)
    {
        var decomposed = value.Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(decomposed.Length);
        var pendingSpace = false;
        foreach (var character in decomposed)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(character) == UnicodeCategory.NonSpacingMark) continue;
            if (char.IsWhiteSpace(character))
            {
                pendingSpace = builder.Length > 0;
                continue;
            }

            if (pendingSpace) builder.Append(' ');
            builder.Append(char.ToLowerInvariant(character));
            pendingSpace = false;
        }

        return builder.ToString();
    }

    private sealed record SearchState(SearchIsbndbEditionsQueryHandler Handler, string Query);

    private sealed class UncacheableSearchException(SearchIsbndbEditionsResult result) : Exception
    {
        public SearchIsbndbEditionsResult Result { get; } = result;
    }
}
