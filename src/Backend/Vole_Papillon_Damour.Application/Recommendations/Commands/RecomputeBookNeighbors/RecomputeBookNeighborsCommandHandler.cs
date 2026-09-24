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
    public async Task<RecomputeBookNeighborsResult> Handle(
        RecomputeBookNeighborsCommand request,
        CancellationToken cancellationToken)
    {
        if (!settings.Enabled || !embeddings.IsConfigured)
        {
            return new RecomputeBookNeighborsResult(0, 0, 0, null);
        }

        var now = dateTimeProvider.UtcNow;
        var visibleIsbns = (await dbContext.Books
                .AsNoTracking()
                .Where(book => !book.IsHiddenFromCatalog && book.RedirectedToIsbn13 == null)
                .Select(book => book.Id)
                .ToListAsync(cancellationToken))
            .Select(isbn13 => isbn13.Value)
            .ToHashSet(StringComparer.Ordinal);

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

        var groupedEditions = WorkGrouping.Group(editions);
        var profilesNeedingEmbedding = new List<(BookSimilarityProfile Profile, string TextHash)>();
        var embeddingTexts = new List<string>();
        foreach (var grouped in groupedEditions)
        {
            var text = SimilarityFeatureBuilder.ComposeText(grouped.Edition, grouped.WorkSummary);
            var textHash = Convert.ToHexString(
                    SHA256.HashData(Encoding.UTF8.GetBytes(text)))
                .ToLowerInvariant();
            var profile = profileByIsbn[grouped.Edition.Isbn13];
            profile.SetProfileText(text, textHash);
            if (profile.NeedsEmbedding)
            {
                profilesNeedingEmbedding.Add((profile, textHash));
                embeddingTexts.Add(text);
            }
        }

        var embeddedCount = 0;
        if (embeddingTexts.Count > 0)
        {
            var vectors = await embeddings.EmbedAsync(embeddingTexts, cancellationToken);
            if (vectors.Count > embeddingTexts.Count)
            {
                throw new InvalidOperationException("The embedding provider returned more vectors than requested.");
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
        var neighborsByIsbn = NeighborComputer.Compute(features, vectorsForBooks, settings.NeighborsPerBook);

        var rows = new List<BookNeighborRow>();
        foreach (var feature in features)
        {
            var neighbors = neighborsByIsbn[feature.Isbn13];
            for (var index = 0; index < neighbors.Count; index++)
            {
                var neighbor = neighbors[index];
                rows.Add(new BookNeighborRow(
                    feature.Isbn13,
                    checked((byte)(index + 1)),
                    neighbor.NeighborIsbn13,
                    neighbor.Score,
                    neighbor.Reason));
            }
        }

        var generationId = Guid.NewGuid();
        await neighborWriter.WriteGenerationAsync(
            generationId,
            rows,
            features.Length,
            now,
            cancellationToken);

        return new RecomputeBookNeighborsResult(features.Length, embeddedCount, rows.Count, generationId);
    }
}
