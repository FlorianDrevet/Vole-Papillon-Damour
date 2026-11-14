using ErrorOr;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Vole_Papillon_Damour.Application.Books.Common;
using Vole_Papillon_Damour.Application.Common.Interfaces.Persistence;
using Vole_Papillon_Damour.Application.Common.Interfaces.Services;
using Vole_Papillon_Damour.Application.Common.Services;
using Vole_Papillon_Damour.Application.Recommendations.Common;
using Vole_Papillon_Damour.Domain.BookAggregate.ValueObjects;
using Vole_Papillon_Damour.Domain.CheckoutPassageAggregate.ValueObjects;
using Vole_Papillon_Damour.Domain.RecommendationAggregate;

namespace Vole_Papillon_Damour.Application.Recommendations.Queries.GetMyRecommendations;

public sealed class GetMyRecommendationsQueryHandler(
    IProjectDbContext dbContext,
    MemberIdentityService memberIdentityService,
    IRecommendationSettings settings,
    IDateTimeProvider dateTimeProvider,
    PersonalSuggestionEngine suggestionEngine)
    : IRequestHandler<GetMyRecommendationsQuery, ErrorOr<MyRecommendationsResult>>
{
    private const int SeedLimit = 200;

    public async Task<ErrorOr<MyRecommendationsResult>> Handle(
        GetMyRecommendationsQuery query,
        CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(query.ExternalId, out var externalId) ||
            externalId == Guid.Empty ||
            string.IsNullOrWhiteSpace(query.Email))
        {
            return Error.Validation("Recommendations.InvalidMember", "A valid member identity is required.");
        }

        var member = await memberIdentityService.EnsureAsync(
            externalId,
            query.Email,
            query.FirstName,
            query.LastName,
            cancellationToken);

        if (!settings.Enabled)
        {
            return Empty(RecommendationStatus.Disabled, query.MinimumHearts ?? DefaultMinimumHearts);
        }

        var preference = await dbContext.MemberRecommendationPreferences
            .AsNoTracking()
            .SingleOrDefaultAsync(candidate => candidate.UserId == member.Id, cancellationToken);
        var minimumHearts = query.MinimumHearts ?? preference?.MinimumHearts ?? DefaultMinimumHearts;

        if (preference is { Enabled: false })
        {
            return Empty(RecommendationStatus.Disabled, minimumHearts);
        }

        var nowUtc = dateTimeProvider.UtcNow;
        if (nowUtc.Kind != DateTimeKind.Utc)
        {
            return Error.Validation(
                "Catalog.InvalidClock",
                "The catalog clock must be expressed in UTC.");
        }

        var favoriteBooks = await dbContext.MemberFavoriteBooks
            .AsNoTracking()
            .Where(favorite => favorite.UserId == member.Id)
            .OrderByDescending(favorite => favorite.AddedAt)
            .ThenBy(favorite => favorite.Isbn13)
            .Take(SeedLimit)
            .ToListAsync(cancellationToken);
        if (favoriteBooks.Count == 0)
        {
            return Empty(RecommendationStatus.NoFavorites, minimumHearts);
        }

        var purchasedLines = await (
                from passage in dbContext.CheckoutPassages.AsNoTracking()
                join line in dbContext.CheckoutPassageLines.AsNoTracking()
                    on passage.Id equals line.CheckoutPassageId
                where passage.UserId == member.Id &&
                      passage.Status == CheckoutPassageStatus.Associated &&
                      line.VoidedAt == null &&
                      line.Isbn13 != null
                select new PurchaseLineSnapshot(line.Isbn13, line.Title))
            .ToListAsync(cancellationToken);
        var purchasedIsbns = purchasedLines
            .Where(line => line.Isbn13.HasValue)
            .Select(line => line.Isbn13!.Value.Value)
            .ToHashSet(StringComparer.Ordinal);
        var purchasedWorkIds = await ReadPurchasedWorkIdsAsync(purchasedLines, cancellationToken);

        var favoriteIsbns = favoriteBooks.Select(favorite => favorite.Isbn13).ToArray();
        var favoriteIsbnValues = favoriteIsbns.Select(isbn => isbn.Value).ToArray();
        var favoriteBookDetails = await dbContext.Books
            .AsNoTracking()
            .Where(book => favoriteIsbns.Contains(book.Id))
            .Select(book => new { Isbn13 = book.Id.Value, book.Title, book.WorkId })
            .ToListAsync(cancellationToken);
        var favoriteDetailByIsbn = favoriteBookDetails.ToDictionary(
            book => book.Isbn13,
            StringComparer.Ordinal);
        var seeds = favoriteBooks
            .Select(favorite => new PersonalSuggestionSeed(
                favorite.Isbn13.Value,
                favoriteDetailByIsbn.TryGetValue(favorite.Isbn13.Value, out var detail)
                    ? detail.Title ?? favorite.Isbn13.Value
                    : favorite.Isbn13.Value))
            .ToArray();
        var favoriteWorkIds = favoriteBookDetails
            .Where(book => !string.IsNullOrWhiteSpace(book.WorkId))
            .Select(book => book.WorkId!)
            .ToHashSet(StringComparer.Ordinal);
        var selectionIsbns = (await dbContext.MemberSelectionItems
                .AsNoTracking()
                .Where(item => item.UserId == member.Id && item.Isbn13 != null)
                .Select(item => item.Isbn13)
                .ToListAsync(cancellationToken))
            .Where(isbn => isbn.HasValue)
            .Select(isbn => isbn!.Value.Value)
            .ToHashSet(StringComparer.Ordinal);

        var generationId = await dbContext.RecommendationGenerations
            .AsNoTracking()
            .Where(generation => generation.Id == 1)
            .Select(generation => generation.CurrentGenerationId)
            .SingleOrDefaultAsync(cancellationToken);
        if (generationId is null)
        {
            return Empty(RecommendationStatus.Enabled, minimumHearts);
        }

        var seedIsbns = seeds.Select(seed => seed.Isbn13).ToArray();
        var neighbors = await dbContext.BookNeighbors
            .AsNoTracking()
            .Where(neighbor =>
                neighbor.GenerationId == generationId.Value &&
                seedIsbns.Contains(neighbor.Isbn13))
            .OrderBy(neighbor => neighbor.Isbn13)
            .ThenBy(neighbor => neighbor.Rank)
            .ToListAsync(cancellationToken);
        var neighborInputs = neighbors
            .Select(neighbor => new PersonalSuggestionNeighbor(
                neighbor.Isbn13,
                neighbor.NeighborIsbn13,
                neighbor.Score,
                neighbor.Reason))
            .ToArray();
        var candidateIsbns = neighbors
            .Select(neighbor => neighbor.NeighborIsbn13)
            .Distinct(StringComparer.Ordinal)
            .Where(isbn => Isbn13.TryCreate(isbn, out _))
            .Select(isbn => Isbn13.TryCreate(isbn, out var parsed) ? parsed : (Isbn13?)null)
            .Where(isbn => isbn.HasValue)
            .Select(isbn => isbn!.Value)
            .ToArray();
        var candidateBooks = candidateIsbns.Length == 0
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
            candidateBooks,
            announcements,
            fairs,
            new HashSet<string>(StringComparer.Ordinal),
            nowUtc);
        var suggestions = suggestionEngine.Suggest(
            seeds,
            neighborInputs,
            new PersonalSuggestionExclusions(
                purchasedIsbns,
                purchasedWorkIds,
                favoriteIsbnValues.ToHashSet(StringComparer.Ordinal),
                favoriteWorkIds,
                selectionIsbns),
            publicBooks,
            minimumHearts,
            query.Limit);

        return new MyRecommendationsResult(RecommendationStatus.Enabled, suggestions, minimumHearts);
    }

    private async Task<HashSet<string>> ReadPurchasedWorkIdsAsync(
        IReadOnlyCollection<PurchaseLineSnapshot> purchasedLines,
        CancellationToken cancellationToken)
    {
        var purchaseIsbnIds = purchasedLines
            .Where(line => line.Isbn13.HasValue)
            .Select(line => line.Isbn13!.Value)
            .Distinct()
            .ToArray();
        var workIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (var isbnBatch in purchaseIsbnIds.Chunk(500))
        {
            var batchWorkIds = await dbContext.Books
                .AsNoTracking()
                .Where(book => isbnBatch.Contains(book.Id) && book.WorkId != null)
                .Select(book => book.WorkId)
                .ToListAsync(cancellationToken);
            foreach (var workId in batchWorkIds)
            {
                if (!string.IsNullOrWhiteSpace(workId))
                {
                    workIds.Add(workId);
                }
            }
        }

        return workIds;
    }

    private int DefaultMinimumHearts => settings.DefaultMinimumHearts is >= 1 and <= 4
        ? settings.DefaultMinimumHearts
        : 3;

    private static MyRecommendationsResult Empty(RecommendationStatus status, int minimumHearts) =>
        new(status, [], minimumHearts);

    private sealed record PurchaseLineSnapshot(Isbn13? Isbn13, string Title);
}
