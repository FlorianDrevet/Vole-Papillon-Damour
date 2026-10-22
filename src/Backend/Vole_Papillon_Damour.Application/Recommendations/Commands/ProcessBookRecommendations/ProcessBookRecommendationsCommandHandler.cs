using MediatR;
using Microsoft.EntityFrameworkCore;
using Vole_Papillon_Damour.Application.Common.Interfaces.Persistence;
using Vole_Papillon_Damour.Application.Common.Interfaces.Services;
using Vole_Papillon_Damour.Application.Recommendations.Commands.RecomputeBookNeighbors;
using Vole_Papillon_Damour.Application.Recommendations.Commands.RefreshBookSimilarityProfiles;
using Vole_Papillon_Damour.Domain.BookAggregate.ValueObjects;
using Vole_Papillon_Damour.Domain.RecommendationAggregate;
using Vole_Papillon_Damour.Domain.RecommendationAggregate.ValueObjects;

namespace Vole_Papillon_Damour.Application.Recommendations.Commands.ProcessBookRecommendations;

public sealed class ProcessBookRecommendationsCommandHandler(
    IProjectDbContext dbContext,
    IRecommendationSettings settings,
    IBookEmbeddingService embeddings,
    ISender sender,
    IDateTimeProvider dateTimeProvider)
    : IRequestHandler<ProcessBookRecommendationsCommand, ProcessBookRecommendationsResult>
{
    public async Task<ProcessBookRecommendationsResult> Handle(
        ProcessBookRecommendationsCommand request,
        CancellationToken cancellationToken)
    {
        if (request.Books.Count == 0 || !settings.Enabled)
        {
            return new ProcessBookRecommendationsResult(0, 0, 0, 0, 0);
        }

        var requestedByIsbn = request.Books
            .Where(item => Isbn13Check.IsCanonical(item.Isbn13) && item.RequestVersion > 0)
            .GroupBy(item => item.Isbn13, StringComparer.Ordinal)
            .Select(group => group.MaxBy(item => item.RequestVersion)!)
            .ToDictionary(item => item.Isbn13, StringComparer.Ordinal);
        if (requestedByIsbn.Count == 0)
        {
            return new ProcessBookRecommendationsResult(0, 0, 0, 0, 0);
        }

        var isbns = requestedByIsbn.Keys.ToArray();
        var jobs = await dbContext.BookRecommendationJobs
            .Where(job => isbns.Contains(job.Isbn13))
            .ToDictionaryAsync(job => job.Isbn13, cancellationToken);
        var books = await dbContext.Books
            .AsNoTracking()
            .Where(book => isbns.Contains(book.Id.Value))
            .Select(book => new
            {
                Isbn13 = book.Id.Value,
                book.MetadataStatus,
                book.IsHiddenFromCatalog,
                book.RedirectedToIsbn13,
            })
            .ToDictionaryAsync(book => book.Isbn13, cancellationToken);

        var started = new List<(BookRecommendationJob Job, int RequestVersion, bool Retired)>();
        var waitingForMetadata = 0;
        var now = dateTimeProvider.UtcNow;
        foreach (var (isbn13, workItem) in requestedByIsbn)
        {
            if (!jobs.TryGetValue(isbn13, out var job) ||
                !books.TryGetValue(isbn13, out var book))
            {
                continue;
            }

            if (book.MetadataStatus == BookMetadataStatus.Pending)
            {
                waitingForMetadata++;
                continue;
            }

            if (!job.TryStart(workItem.RequestVersion, now))
            {
                continue;
            }

            var retired = book.IsHiddenFromCatalog || book.RedirectedToIsbn13 is not null;
            started.Add((job, workItem.RequestVersion, retired));
        }

        if (started.Count == 0)
        {
            return new ProcessBookRecommendationsResult(0, 0, 0, 0, waitingForMetadata);
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        var failed = 0;
        var noticeFound = new List<(BookRecommendationJob Job, int RequestVersion)>();
        var withoutNotice = new List<(BookRecommendationJob Job, int RequestVersion)>();
        foreach (var entry in started)
        {
            if (entry.Retired)
            {
                withoutNotice.Add((entry.Job, entry.RequestVersion));
                continue;
            }

            if (entry.Job.Stage == BookRecommendationJobStage.Notice)
            {
                try
                {
                    await sender.Send(
                        new RefreshBookSimilarityProfilesCommand(entry.Job.Isbn13),
                        cancellationToken);
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    throw;
                }
                catch (Exception exception)
                {
                    entry.Job.Fail(
                        entry.RequestVersion,
                        BookRecommendationJobStage.Notice,
                        Describe(exception),
                        dateTimeProvider.UtcNow);
                    failed++;
                    continue;
                }
            }

            var profile = await dbContext.BookSimilarityProfiles
                .AsNoTracking()
                .SingleOrDefaultAsync(
                    candidate => candidate.Isbn13 == entry.Job.Isbn13,
                    cancellationToken);
            if (profile?.NoticeFound == true)
            {
                if (entry.Job.Stage == BookRecommendationJobStage.Notice)
                {
                    entry.Job.AdvanceTo(BookRecommendationJobStage.Embedding);
                }

                noticeFound.Add((entry.Job, entry.RequestVersion));
            }
            else
            {
                withoutNotice.Add((entry.Job, entry.RequestVersion));
            }
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        var embeddedFailure = 0;
        if (noticeFound.Count > 0 && !embeddings.IsConfigured)
        {
            foreach (var (job, requestVersion) in noticeFound)
            {
                job.Fail(
                    requestVersion,
                    BookRecommendationJobStage.Embedding,
                    "Le service d’embedding n’est pas configuré.",
                    dateTimeProvider.UtcNow);
            }

            embeddedFailure = noticeFound.Count;
            await dbContext.SaveChangesAsync(cancellationToken);
            noticeFound.Clear();
        }

        try
        {
            await sender.Send(
                new RecomputeBookNeighborsCommand(started.Select(entry => entry.Job.Isbn13).ToArray()),
                cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            var profilesNeedingEmbedding = await dbContext.BookSimilarityProfiles
                .AsNoTracking()
                .Where(profile => noticeFound.Select(item => item.Job.Isbn13).Contains(profile.Isbn13))
                .Select(profile => new { profile.Isbn13, profile.NeedsEmbedding })
                .ToDictionaryAsync(profile => profile.Isbn13, cancellationToken);
            foreach (var (job, requestVersion) in noticeFound)
            {
                var stage = profilesNeedingEmbedding.TryGetValue(job.Isbn13, out var profile) &&
                            profile.NeedsEmbedding
                    ? BookRecommendationJobStage.Embedding
                    : BookRecommendationJobStage.Neighbors;
                job.Fail(requestVersion, stage, Describe(exception), dateTimeProvider.UtcNow);
            }

            foreach (var (job, requestVersion) in withoutNotice)
            {
                job.Fail(
                    requestVersion,
                    BookRecommendationJobStage.Neighbors,
                    Describe(exception),
                    dateTimeProvider.UtcNow);
            }

            await dbContext.SaveChangesAsync(cancellationToken);
            return new ProcessBookRecommendationsResult(
                started.Count,
                0,
                0,
                failed + noticeFound.Count + withoutNotice.Count,
                waitingForMetadata);
        }

        foreach (var (job, requestVersion) in noticeFound)
        {
            job.AdvanceTo(BookRecommendationJobStage.Neighbors);
            job.Complete(requestVersion, BookRecommendationJobStatus.Ready, dateTimeProvider.UtcNow);
        }

        foreach (var (job, requestVersion) in withoutNotice)
        {
            job.Complete(requestVersion, BookRecommendationJobStatus.NoNotice, dateTimeProvider.UtcNow);
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        return new ProcessBookRecommendationsResult(
            started.Count,
            noticeFound.Count,
            withoutNotice.Count,
            failed + embeddedFailure,
            waitingForMetadata);
    }

    private static string Describe(Exception exception) =>
        $"{exception.GetType().Name}: {exception.Message}";
}
