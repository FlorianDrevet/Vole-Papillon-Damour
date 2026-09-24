using System.Text.Json;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Vole_Papillon_Damour.Application.Common.Interfaces.Persistence;
using Vole_Papillon_Damour.Application.Common.Interfaces.Services;
using Vole_Papillon_Damour.Domain.RecommendationAggregate;

namespace Vole_Papillon_Damour.Application.Recommendations.Commands.RefreshBookSimilarityProfiles;

public sealed class RefreshBookSimilarityProfilesCommandHandler(
    IProjectDbContext dbContext,
    IBibliographicNoticeReader noticeReader,
    IRecommendationSettings settings,
    IDateTimeProvider dateTimeProvider)
    : IRequestHandler<RefreshBookSimilarityProfilesCommand, RefreshBookSimilarityProfilesResult>
{
    private static readonly TimeSpan NoticeRefreshInterval = TimeSpan.FromDays(90);

    public async Task<RefreshBookSimilarityProfilesResult> Handle(
        RefreshBookSimilarityProfilesCommand request,
        CancellationToken cancellationToken)
    {
        if (!settings.Enabled)
        {
            return new RefreshBookSimilarityProfilesResult(0, 0, 0);
        }

        var now = dateTimeProvider.UtcNow;
        var refreshBefore = now - NoticeRefreshInterval;
        var books = await dbContext.Books
            .AsNoTracking()
            .Where(book => !book.IsHiddenFromCatalog && book.RedirectedToIsbn13 == null)
            .Select(book => new { Isbn13 = book.Id, book.UpdatedAt })
            .ToListAsync(cancellationToken);
        var profileStatuses = await dbContext.BookSimilarityProfiles
            .AsNoTracking()
            .Select(profile => new { profile.Isbn13, profile.NoticeFetchedAt })
            .ToDictionaryAsync(profile => profile.Isbn13, cancellationToken);

        var candidates = books
            .Where(book =>
            {
                var isbn13 = book.Isbn13.Value;
                if (!profileStatuses.TryGetValue(isbn13, out var profile))
                {
                    return true;
                }

                return profile.NoticeFetchedAt is null ||
                       profile.NoticeFetchedAt < refreshBefore ||
                       profile.NoticeFetchedAt < book.UpdatedAt;
            })
            .OrderBy(book =>
                profileStatuses.TryGetValue(book.Isbn13.Value, out var profile)
                    ? profile.NoticeFetchedAt ?? DateTime.MinValue
                    : DateTime.MinValue)
            .ThenBy(book => book.UpdatedAt)
            .Take(Math.Max(0, settings.ProfileBatchSize))
            .ToArray();

        if (candidates.Length == 0)
        {
            return new RefreshBookSimilarityProfilesResult(0, 0, 0);
        }

        var candidateIsbns = candidates.Select(book => book.Isbn13.Value).ToArray();
        var profiles = await dbContext.BookSimilarityProfiles
            .Where(profile => candidateIsbns.Contains(profile.Isbn13))
            .ToDictionaryAsync(profile => profile.Isbn13, cancellationToken);

        var found = 0;
        var notFound = 0;
        foreach (var candidate in candidates)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var isbn13 = candidate.Isbn13.Value;
            var edition = await noticeReader.ReadAsync(isbn13, cancellationToken);
            if (!profiles.TryGetValue(isbn13, out var profile))
            {
                profile = BookSimilarityProfile.Create(isbn13);
                profiles.Add(isbn13, profile);
                dbContext.BookSimilarityProfiles.Add(profile);
            }

            if (edition is null)
            {
                profile.RecordNotice(null, false, now);
                notFound++;
            }
            else
            {
                profile.RecordNotice(JsonSerializer.Serialize(edition), true, now);
                found++;
            }
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        return new RefreshBookSimilarityProfilesResult(candidates.Length, found, notFound);
    }
}
