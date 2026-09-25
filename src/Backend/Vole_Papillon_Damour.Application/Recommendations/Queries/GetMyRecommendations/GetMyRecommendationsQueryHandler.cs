using ErrorOr;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Vole_Papillon_Damour.Application.Books.Common;
using Vole_Papillon_Damour.Application.Common.Interfaces.Persistence;
using Vole_Papillon_Damour.Application.Common.Interfaces.Services;
using Vole_Papillon_Damour.Application.Common.Services;
using Vole_Papillon_Damour.Domain.BookAggregate.ValueObjects;
using Vole_Papillon_Damour.Domain.CheckoutPassageAggregate.ValueObjects;
using Vole_Papillon_Damour.Domain.RecommendationAggregate;

namespace Vole_Papillon_Damour.Application.Recommendations.Queries.GetMyRecommendations;

public sealed class GetMyRecommendationsQueryHandler(
    IProjectDbContext dbContext,
    MemberIdentityService memberIdentityService,
    IRecommendationSettings settings,
    IDateTimeProvider dateTimeProvider)
    : IRequestHandler<GetMyRecommendationsQuery, ErrorOr<MyRecommendationsResult>>
{
    private const int SeedLimit = 20;

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
            return Empty(RecommendationStatus.Disabled);
        }

        var preference = await dbContext.MemberRecommendationPreferences
            .AsNoTracking()
            .SingleOrDefaultAsync(candidate => candidate.UserId == member.Id, cancellationToken);
        if (preference is { Enabled: false })
        {
            return Empty(RecommendationStatus.Disabled);
        }

        var nowUtc = dateTimeProvider.UtcNow;
        if (nowUtc.Kind != DateTimeKind.Utc)
        {
            return Error.Validation(
                "Catalog.InvalidClock",
                "The catalog clock must be expressed in UTC.");
        }

        var purchasedLines = await (
                from passage in dbContext.CheckoutPassages.AsNoTracking()
                join line in dbContext.CheckoutPassageLines.AsNoTracking()
                    on passage.Id equals line.CheckoutPassageId
                where passage.UserId == member.Id &&
                      passage.Status == CheckoutPassageStatus.Associated &&
                      line.VoidedAt == null &&
                      line.Isbn13 != null
                orderby line.OccurredAt descending, line.Id descending
                select new PurchaseLineSnapshot(line.Isbn13, line.Title, line.OccurredAt))
            .ToListAsync(cancellationToken);

        var purchasedIsbns = new HashSet<string>(StringComparer.Ordinal);
        var seeds = new List<Seed>(SeedLimit);
        foreach (var line in purchasedLines)
        {
            if (line.Isbn13 is not { } isbn)
            {
                continue;
            }

            purchasedIsbns.Add(isbn.Value);
            if (seeds.Count >= SeedLimit || !seeds.All(seed => seed.Isbn13 != isbn.Value))
            {
                continue;
            }

            seeds.Add(new Seed(isbn.Value, line.Title, seeds.Count));
        }

        if (seeds.Count == 0)
        {
            return Empty(RecommendationStatus.NoPurchases);
        }

        var generationId = await dbContext.RecommendationGenerations
            .AsNoTracking()
            .Where(generation => generation.Id == 1)
            .Select(generation => generation.CurrentGenerationId)
            .SingleOrDefaultAsync(cancellationToken);
        if (generationId is null)
        {
            return Empty(RecommendationStatus.Enabled);
        }

        var purchasedWorkIds = await ReadPurchasedWorkIdsAsync(
            purchasedLines,
            cancellationToken);
        var selectionIsbns = (await dbContext.MemberSelectionItems
                .AsNoTracking()
                .Where(item => item.UserId == member.Id && item.Isbn13 != null)
                .Select(item => item.Isbn13)
                .ToListAsync(cancellationToken))
            .Where(isbn => isbn.HasValue)
            .Select(isbn => isbn!.Value.Value)
            .ToHashSet(StringComparer.Ordinal);
        var seedByIsbn = seeds.ToDictionary(seed => seed.Isbn13, StringComparer.Ordinal);
        var seedIsbns = seeds.Select(seed => seed.Isbn13).ToArray();
        var neighbors = await dbContext.BookNeighbors
            .AsNoTracking()
            .Where(neighbor =>
                neighbor.GenerationId == generationId.Value &&
                seedIsbns.Contains(neighbor.Isbn13))
            .OrderBy(neighbor => neighbor.Isbn13)
            .ThenBy(neighbor => neighbor.Rank)
            .ToListAsync(cancellationToken);

        var candidates = new Dictionary<string, CandidateScore>(StringComparer.Ordinal);
        foreach (var neighbor in neighbors)
        {
            if (!seedByIsbn.TryGetValue(neighbor.Isbn13, out var seed))
            {
                continue;
            }

            var contribution = neighbor.Score * seed.Weight(seeds.Count);
            if (!candidates.TryGetValue(neighbor.NeighborIsbn13, out var candidate))
            {
                candidate = new CandidateScore();
                candidates.Add(neighbor.NeighborIsbn13, candidate);
            }

            candidate.TotalScore += contribution;
            candidate.HasQualifyingNeighbor |= neighbor.Score >= settings.SimilarMinScore;
            if (contribution > candidate.StrongestContribution ||
                (contribution == candidate.StrongestContribution && seed.Index < candidate.Seed.Index))
            {
                candidate.StrongestContribution = contribution;
                candidate.Seed = seed;
                candidate.Reason = neighbor.Reason;
            }
        }

        if (candidates.Count == 0)
        {
            return Empty(RecommendationStatus.Enabled);
        }

        var candidateIsbns = candidates.Keys
            .Where(isbn =>
                !purchasedIsbns.Contains(isbn) &&
                !selectionIsbns.Contains(isbn) &&
                candidates[isbn].HasQualifyingNeighbor &&
                Isbn13.TryCreate(isbn, out _))
            .Select(isbn => Isbn13.TryCreate(isbn, out var parsed) ? parsed : (Isbn13?)null)
            .Where(isbn => isbn.HasValue)
            .Select(isbn => isbn!.Value)
            .ToArray();
        if (candidateIsbns.Length == 0)
        {
            return Empty(RecommendationStatus.Enabled);
        }

        var books = await dbContext.Books
            .AsNoTracking()
            .Where(book =>
                candidateIsbns.Contains(book.Id) &&
                !book.IsHiddenFromCatalog &&
                book.RedirectedToIsbn13 == null)
            .ToListAsync(cancellationToken);
        var announcements = await dbContext.BookAnnouncements
            .AsNoTracking()
            .Where(announcement => candidateIsbns.Contains(announcement.Isbn13))
            .ToListAsync(cancellationToken);
        var fairs = await dbContext.AssoEvents
            .AsNoTracking()
            .ToReferencedFairListAsync(announcements, cancellationToken);
        var projectedBooks = PublicCatalogProjector.Project(
                books,
                announcements,
                fairs,
                new HashSet<string>(StringComparer.Ordinal),
                nowUtc)
            .ToDictionary(book => book.Isbn13, StringComparer.Ordinal);

        var orderedCandidates = candidates
            .Where(pair =>
                pair.Value.HasQualifyingNeighbor &&
                !purchasedIsbns.Contains(pair.Key) &&
                !selectionIsbns.Contains(pair.Key))
            .OrderByDescending(pair => pair.Value.TotalScore)
            .ThenBy(pair => pair.Key, StringComparer.Ordinal);
        var results = new List<PersonalRecommendation>(Math.Max(0, query.Limit));
        foreach (var (isbn, candidate) in orderedCandidates)
        {
            if (!projectedBooks.TryGetValue(isbn, out var book) ||
                (book.QuantityAvailable <= 0 && book.QuantityAnnounced <= 0) ||
                (book.WorkId is not null && purchasedWorkIds.Contains(book.WorkId)))
            {
                continue;
            }

            results.Add(new PersonalRecommendation(book, candidate.Reason, candidate.Seed.Title));
            if (results.Count >= query.Limit)
            {
                break;
            }
        }

        return new MyRecommendationsResult(RecommendationStatus.Enabled, results);
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

    private static MyRecommendationsResult Empty(RecommendationStatus status) => new(status, []);

    private sealed record PurchaseLineSnapshot(Isbn13? Isbn13, string Title, DateTime OccurredAt);

    private sealed record Seed(string Isbn13, string Title, int Index)
    {
        public float Weight(int seedCount) =>
            1f - 0.5f * Index / Math.Max(1, seedCount - 1);
    }

    private sealed class CandidateScore
    {
        public float TotalScore { get; set; }
        public float StrongestContribution { get; set; } = float.NegativeInfinity;
        public bool HasQualifyingNeighbor { get; set; }
        public Seed Seed { get; set; } = null!;
        public Vole_Papillon_Damour.Domain.RecommendationAggregate.ValueObjects.NeighborReason Reason { get; set; }
    }
}
