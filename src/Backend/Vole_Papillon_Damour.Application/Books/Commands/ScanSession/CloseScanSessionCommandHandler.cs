using ErrorOr;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Vole_Papillon_Damour.Application.Books.Common;
using Vole_Papillon_Damour.Application.Common.Interfaces.Persistence;
using Vole_Papillon_Damour.Application.Common.Interfaces.Services;
using Vole_Papillon_Damour.Domain.Common.Errors;
using Vole_Papillon_Damour.Domain.RecommendationAggregate;
using Vole_Papillon_Damour.Domain.ScanSessionAggregate.ValueObjects;

namespace Vole_Papillon_Damour.Application.Books.Commands.ScanSession;

public sealed class CloseScanSessionCommandHandler(
    IProjectDbContext dbContext,
    IDateTimeProvider dateTimeProvider,
    IBookAlertOutbox bookAlertOutbox,
    IBookRecommendationWorkQueue? recommendationWorkQueue = null)
    : IRequestHandler<CloseScanSessionCommand, ErrorOr<ScanSessionResult>>
{
    public async Task<ErrorOr<ScanSessionResult>> Handle(
        CloseScanSessionCommand command,
        CancellationToken cancellationToken)
    {
        var endedAt = dateTimeProvider.UtcNow;
        if (endedAt.Kind != DateTimeKind.Utc)
        {
            return Errors.Book.InvalidScanTimestamp();
        }

        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);

        var session = await dbContext.ScanSessions
            .SingleOrDefaultAsync(
                candidate => candidate.Id == command.ScanSessionId,
                cancellationToken);

        if (session is null)
        {
            return Errors.Book.ScanSessionNotFound(command.ScanSessionId.Value);
        }

        if (session.VolunteerId != command.VolunteerId)
        {
            return Errors.Book.ScanSessionNotFound(command.ScanSessionId.Value);
        }

        if (session.Close(command.CloseReason, endedAt))
        {
            await bookAlertOutbox.QueueForSessionAsync(
                session.Id,
                endedAt,
                cancellationToken);
        }

        var recommendationWork = Array.Empty<BookRecommendationQueueItem>();
        if (session.Status == ScanSessionStatus.Completed &&
            recommendationWorkQueue is not null)
        {
            var isbns = await dbContext.BookMovements
                .AsNoTracking()
                .Where(movement => movement.ScanSessionId == session.Id)
                .Select(movement => movement.Isbn13.Value)
                .Distinct()
                .ToArrayAsync(cancellationToken);
            if (isbns.Length > 0)
            {
                var jobs = await dbContext.BookRecommendationJobs
                    .Where(job => isbns.Contains(job.Isbn13))
                    .ToDictionaryAsync(job => job.Isbn13, cancellationToken);
                var requested = new List<BookRecommendationQueueItem>(isbns.Length);
                foreach (var isbn13 in isbns)
                {
                    if (!jobs.TryGetValue(isbn13, out var job))
                    {
                        job = BookRecommendationJob.Create(isbn13);
                        dbContext.BookRecommendationJobs.Add(job);
                        jobs.Add(isbn13, job);
                    }

                    if (job.Request(endedAt))
                    {
                        requested.Add(new BookRecommendationQueueItem(isbn13, job.RequestVersion));
                    }
                }

                recommendationWork = requested.ToArray();
            }
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        await transaction.CommitAsync(cancellationToken);
        if (recommendationWorkQueue is not null && recommendationWork.Length > 0)
        {
            await recommendationWorkQueue.EnqueueBooksAsync(recommendationWork, cancellationToken);
        }

        return ScanSessionResult.From(session);
    }
}
