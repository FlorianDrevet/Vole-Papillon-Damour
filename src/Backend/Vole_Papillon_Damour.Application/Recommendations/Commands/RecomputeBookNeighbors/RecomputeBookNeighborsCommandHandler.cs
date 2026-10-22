using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Vole_Papillon_Damour.Application.Common.Interfaces.Persistence;
using Vole_Papillon_Damour.Application.Common.Interfaces.Services;
using Vole_Papillon_Damour.Application.Recommendations.Similarity;
using Vole_Papillon_Damour.Domain.RecommendationAggregate;

namespace Vole_Papillon_Damour.Application.Recommendations.Commands.RecomputeBookNeighbors;

public sealed class RecomputeBookNeighborsCommandHandler(
    IProjectDbContext dbContext,
    IRecommendationSettings settings,
    IBookEmbeddingService embeddings,
    IBookNeighborWriter neighborWriter,
    IDateTimeProvider dateTimeProvider)
    : IRequestHandler<RecomputeBookNeighborsCommand, RecomputeBookNeighborsResult>
{
    private const string FeatureTextVersion = "similarity-features-v1";

    public async Task<RecomputeBookNeighborsResult> Handle(
        RecomputeBookNeighborsCommand request,
        CancellationToken cancellationToken)
    {
        if (!settings.Enabled || !embeddings.IsConfigured)
        {
            return new RecomputeBookNeighborsResult(0, 0, 0, null);
        }

        var now = dateTimeProvider.UtcNow;
        var visibleBooks = await dbContext.Books
                .AsNoTracking()
                .Where(book => !book.IsHiddenFromCatalog && book.RedirectedToIsbn13 == null)
                .Select(book => new { Isbn13 = book.Id, book.Summary })
                .ToListAsync(cancellationToken);
        var summariesByIsbn = visibleBooks.ToDictionary(
            book => book.Isbn13.Value,
            book => book.Summary,
            StringComparer.Ordinal);
        var visibleIsbns = summariesByIsbn.Keys.ToHashSet(StringComparer.Ordinal);

        var profiles = await dbContext.BookSimilarityProfiles
            .Where(profile => profile.NoticeFound)
            .ToListAsync(cancellationToken);
        var eligibleProfiles = profiles
            .Where(profile => visibleIsbns.Contains(profile.Isbn13))
            .OrderBy(profile => profile.Isbn13, StringComparer.Ordinal)
            .ToArray();

        var profileByIsbn = eligibleProfiles.ToDictionary(profile => profile.Isbn13, StringComparer.Ordinal);
        var editions = eligibleProfiles.Select(profile =>
        {
            if (string.IsNullOrWhiteSpace(profile.NoticeJson))
            {
                throw new InvalidOperationException($"Found notice for {profile.Isbn13} has no JSON payload.");
            }

            var edition = JsonSerializer.Deserialize<SimilarityEdition>(profile.NoticeJson)
                          ?? throw new InvalidOperationException($"Found notice for {profile.Isbn13} is empty.");
            if (!string.Equals(edition.Isbn13, profile.Isbn13, StringComparison.Ordinal))
            {
                throw new InvalidOperationException($"Notice ISBN does not match profile {profile.Isbn13}.");
            }

            return edition;
        }).ToArray();

        var groupedEditions = WorkGrouping.Group(editions, summariesByIsbn);
        var requestedIsbns = request.Isbn13s?.ToHashSet(StringComparer.Ordinal);
        var requestedFeatures = requestedIsbns is null
            ? Array.Empty<SimilarityFeatures>()
            : groupedEditions
                .Where(grouped => requestedIsbns.Contains(grouped.Edition.Isbn13))
                .Select(grouped => grouped.Features)
                .ToArray();
        var profilesNeedingEmbedding = new List<(BookSimilarityProfile Profile, string TextHash)>();
        var embeddingTexts = new List<string>();
        var changedIsbns = new HashSet<string>(StringComparer.Ordinal);
        foreach (var grouped in groupedEditions)
        {
            var text = SimilarityFeatureBuilder.ComposeText(grouped.Edition, grouped.WorkSummary);
            var textHash = Convert.ToHexString(
                    SHA256.HashData(Encoding.UTF8.GetBytes(
                        $"{FeatureTextVersion}\n{settings.EmbeddingModelVersion ?? "unspecified"}\n{text}")))
                .ToLowerInvariant();
            var profile = profileByIsbn[grouped.Edition.Isbn13];
            var requestedBook = requestedIsbns is null || requestedIsbns.Contains(profile.Isbn13);
            var sharesRequestedWork = requestedFeatures.Any(target =>
                SimilarityFeatureBuilder.SameWork(grouped.Features, target));
            if (requestedBook || sharesRequestedWork)
            {
                profile.SetProfileText(text, textHash);
                if (profile.NeedsEmbedding)
                {
                    profilesNeedingEmbedding.Add((profile, textHash));
                    embeddingTexts.Add(text);
                    changedIsbns.Add(profile.Isbn13);
                }
            }
        }

        var embeddedCount = 0;
        if (embeddingTexts.Count > 0)
        {
            var vectors = await embeddings.EmbedAsync(embeddingTexts, cancellationToken);
            if (vectors.Count != embeddingTexts.Count)
            {
                throw new InvalidOperationException(
                    $"The embedding provider returned {vectors.Count} vectors for {embeddingTexts.Count} texts.");
            }

            for (var index = 0; index < vectors.Count; index++)
            {
                profilesNeedingEmbedding[index].Profile.RecordEmbedding(
                    EmbeddingBytes.ToBytes(vectors[index]),
                    profilesNeedingEmbedding[index].TextHash,
                    now);
                embeddedCount++;
            }
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        var computedEditions = groupedEditions
            .Where(grouped =>
            {
                var profile = profileByIsbn[grouped.Edition.Isbn13];
                return profile.Embedding is not null &&
                       string.Equals(profile.EmbeddedTextHash, profile.ProfileTextHash, StringComparison.Ordinal);
            })
            .ToArray();
        var features = computedEditions.Select(grouped => grouped.Features).ToArray();
        var vectorsForBooks = computedEditions
            .Select(grouped => EmbeddingBytes.ToFloats(profileByIsbn[grouped.Edition.Isbn13].Embedding!))
            .ToArray();
        Dictionary<string, IReadOnlyList<NeighborCandidate>> neighborsByIsbn;
        if (requestedIsbns is null)
        {
            neighborsByIsbn = new Dictionary<string, IReadOnlyList<NeighborCandidate>>(
                NeighborComputer.Compute(features, vectorsForBooks, settings.NeighborsPerBook),
                StringComparer.Ordinal);
        }
        else
        {
            var generation = await dbContext.RecommendationGenerations
                .AsNoTracking()
                .SingleOrDefaultAsync(item => item.Id == 1, cancellationToken);
            var existingRows = generation?.CurrentGenerationId is { } currentGenerationId
                ? await dbContext.BookNeighbors
                    .AsNoTracking()
                    .Where(row => row.GenerationId == currentGenerationId)
                    .OrderBy(row => row.Isbn13)
                    .ThenBy(row => row.Rank)
                    .ToListAsync(cancellationToken)
                : [];
            var currentNeighbors = existingRows
                .GroupBy(row => row.Isbn13, StringComparer.Ordinal)
                .ToDictionary(
                    group => group.Key,
                    group => (IReadOnlyList<NeighborCandidate>)group
                        .Select(row => new NeighborCandidate(row.NeighborIsbn13, row.Score, row.Reason))
                        .ToArray(),
                    StringComparer.Ordinal);
            var changedTargets = requestedIsbns.Concat(changedIsbns)
                .Distinct(StringComparer.Ordinal)
                .ToArray();
            neighborsByIsbn = new Dictionary<string, IReadOnlyList<NeighborCandidate>>(
                NeighborComputer.ComputeIncremental(
                    features,
                    vectorsForBooks,
                    changedTargets,
                    currentNeighbors,
                    settings.NeighborsPerBook),
                StringComparer.Ordinal);
        }

        var rows = new List<BookNeighborRow>();
        foreach (var (isbn13, neighbors) in neighborsByIsbn)
        {
            for (var index = 0; index < neighbors.Count; index++)
            {
                var neighbor = neighbors[index];
                rows.Add(new BookNeighborRow(
                    isbn13,
                    checked((byte)(index + 1)),
                    neighbor.NeighborIsbn13,
                    neighbor.Score,
                    neighbor.Reason));
            }
        }

        var generationId = Guid.NewGuid();
        if (requestedIsbns is null)
        {
            await neighborWriter.WriteGenerationAsync(
                generationId,
                rows,
                features.Length,
                now,
                cancellationToken);
        }
        else
        {
            await neighborWriter.WriteIncrementalGenerationAsync(
                generationId,
                neighborsByIsbn.Keys.ToArray(),
                rows,
                features.Length,
                now,
                cancellationToken);
        }

        return new RecomputeBookNeighborsResult(
            requestedIsbns is null ? features.Length : requestedIsbns.Count,
            embeddedCount,
            rows.Count,
            generationId);
    }
}
