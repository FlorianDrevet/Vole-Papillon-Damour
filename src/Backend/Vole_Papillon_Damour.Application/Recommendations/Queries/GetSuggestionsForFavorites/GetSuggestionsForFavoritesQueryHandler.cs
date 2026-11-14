using ErrorOr;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Vole_Papillon_Damour.Application.Books.Common;
using Vole_Papillon_Damour.Application.Common.Interfaces.Persistence;
using Vole_Papillon_Damour.Application.Common.Interfaces.Services;
using Vole_Papillon_Damour.Application.Recommendations.Common;
using Vole_Papillon_Damour.Application.Recommendations.Queries.GetMyRecommendations;
using Vole_Papillon_Damour.Domain.BookAggregate.ValueObjects;

namespace Vole_Papillon_Damour.Application.Recommendations.Queries.GetSuggestionsForFavorites;

public sealed class GetSuggestionsForFavoritesQueryHandler(
    IProjectDbContext dbContext,
    IRecommendationSettings settings,
    IDateTimeProvider dateTimeProvider,
    PersonalSuggestionEngine suggestionEngine)
    : IRequestHandler<GetSuggestionsForFavoritesQuery, ErrorOr<SuggestionsForFavoritesResult>>
{
    public async Task<ErrorOr<SuggestionsForFavoritesResult>> Handle(
        GetSuggestionsForFavoritesQuery query,
        CancellationToken cancellationToken)
    {
        if (!settings.Enabled)
        {
            return new SuggestionsForFavoritesResult(
                RecommendationStatus.Disabled,
                [],
                [],
                query.MinimumHearts);
        }

        var nowUtc = dateTimeProvider.UtcNow;
        if (nowUtc.Kind != DateTimeKind.Utc)
        {
            return Error.Validation(
                "Catalog.InvalidClock",
                "The catalog clock must be expressed in UTC.");
        }

        var requestedIsbns = query.Isbn13s
            .Where(isbn => Isbn13.TryCreate(isbn, out var parsed) && parsed.Value == isbn)
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        var requestedIds = requestedIsbns
            .Select(isbn => Isbn13.TryCreate(isbn, out var parsed) ? parsed : default)
            .ToArray();
        var seedBooks = await dbContext.Books
            .AsNoTracking()
            .Where(book => requestedIds.Contains(book.Id))
            .Select(book => new { Isbn13 = book.Id.Value, book.Title, book.WorkId })
            .ToListAsync(cancellationToken);
        var seedBookByIsbn = seedBooks.ToDictionary(book => book.Isbn13, StringComparer.Ordinal);
        var jobs = await dbContext.BookRecommendationJobs
            .AsNoTracking()
            .Where(job => requestedIsbns.Contains(job.Isbn13))
            .ToDictionaryAsync(job => job.Isbn13, StringComparer.Ordinal, cancellationToken);
        var seedStates = requestedIsbns
            .Select(isbn =>
            {
                if (!seedBookByIsbn.ContainsKey(isbn))
                {
                    return new FavoriteSeedProcessingState(isbn, "Unknown", "Notice");
                }

                return jobs.TryGetValue(isbn, out var job)
                    ? new FavoriteSeedProcessingState(isbn, job.Status.ToString(), job.Stage.ToString())
                    : new FavoriteSeedProcessingState(isbn, "Pending", "Notice");
            })
            .ToArray();
        if (requestedIsbns.Length == 0 || seedBooks.Count == 0)
        {
            return new SuggestionsForFavoritesResult(
                RecommendationStatus.NoFavorites,
                [],
                seedStates,
                query.MinimumHearts);
        }

        var generationId = await dbContext.RecommendationGenerations
            .AsNoTracking()
            .Where(generation => generation.Id == 1)
            .Select(generation => generation.CurrentGenerationId)
            .SingleOrDefaultAsync(cancellationToken);
        if (generationId is null)
        {
            return new SuggestionsForFavoritesResult(
                RecommendationStatus.Enabled,
                [],
                seedStates,
                query.MinimumHearts);
        }

        var neighbors = await dbContext.BookNeighbors
            .AsNoTracking()
            .Where(neighbor =>
                neighbor.GenerationId == generationId.Value &&
                requestedIsbns.Contains(neighbor.Isbn13))
            .OrderBy(neighbor => neighbor.Isbn13)
            .ThenBy(neighbor => neighbor.Rank)
            .ToListAsync(cancellationToken);
        var candidateIsbns = neighbors
            .Select(neighbor => neighbor.NeighborIsbn13)
            .Distinct(StringComparer.Ordinal)
            .Where(isbn => Isbn13.TryCreate(isbn, out _))
            .Select(isbn => Isbn13.TryCreate(isbn, out var parsed) ? parsed : (Isbn13?)null)
            .Where(isbn => isbn.HasValue)
            .Select(isbn => isbn!.Value)
            .ToArray();
        var candidates = candidateIsbns.Length == 0
            ? []
            : await dbContext.Books
                .AsNoTracking()
                .Where(book =>
                    candidateIsbns.Contains(book.Id) &&
                    !book.IsFavoriteReference &&
                    !book.IsHiddenFromCatalog &&
                    book.RedirectedToIsbn13 == null &&
                    (book.MetadataStatus == BookMetadataStatus.Resolved ||
                     book.MetadataStatus == BookMetadataStatus.Manual) &&
                    book.Title != null &&
                    book.Title.Trim() != string.Empty)
                .ToListAsync(cancellationToken);
        var announcements = candidateIsbns.Length == 0
            ? []
            : await dbContext.BookAnnouncements
                .AsNoTracking()
                .Where(announcement => candidateIsbns.Contains(announcement.Isbn13))
                .ToListAsync(cancellationToken);
        var fairs = await dbContext.AssoEvents
            .AsNoTracking()
            .ToReferencedFairListAsync(announcements, cancellationToken);
        var publicBooks = PublicCatalogProjector.Project(
            candidates,
            announcements,
            fairs,
            new HashSet<string>(StringComparer.Ordinal),
            nowUtc);
        var seeds = requestedIsbns
            .Where(seedBookByIsbn.ContainsKey)
            .Select(isbn => new PersonalSuggestionSeed(
                isbn,
                seedBookByIsbn[isbn].Title ?? isbn))
            .ToArray();
        var favoriteWorkIds = seedBooks
            .Where(book => !string.IsNullOrWhiteSpace(book.WorkId))
            .Select(book => book.WorkId!)
            .ToHashSet(StringComparer.Ordinal);
        var suggestions = suggestionEngine.Suggest(
            seeds,
            neighbors.Select(neighbor => new PersonalSuggestionNeighbor(
                neighbor.Isbn13,
                neighbor.NeighborIsbn13,
                neighbor.Score,
                neighbor.Reason)),
            new PersonalSuggestionExclusions(
                new HashSet<string>(StringComparer.Ordinal),
                new HashSet<string>(StringComparer.Ordinal),
                requestedIsbns.ToHashSet(StringComparer.Ordinal),
                favoriteWorkIds,
                new HashSet<string>(StringComparer.Ordinal)),
            publicBooks,
            query.MinimumHearts,
            settings.PersonalMaxCount);

        return new SuggestionsForFavoritesResult(
            RecommendationStatus.Enabled,
            suggestions,
            seedStates,
            query.MinimumHearts);
    }
}
