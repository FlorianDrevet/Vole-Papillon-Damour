using ErrorOr;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Vole_Papillon_Damour.Application.Common.Interfaces.Persistence;
using Vole_Papillon_Damour.Application.Common.Interfaces.Services;
using Vole_Papillon_Damour.Domain.BookAggregate.ValueObjects;
using Vole_Papillon_Damour.Domain.Common.Errors;
using Vole_Papillon_Damour.Domain.RecommendationAggregate;

namespace Vole_Papillon_Damour.Application.Books.Commands.Admin;

public sealed class RetryBookRecommendationJobCommandHandler(
    IProjectDbContext dbContext,
    IBookRecommendationWorkQueue workQueue,
    IDateTimeProvider dateTimeProvider)
    : IRequestHandler<RetryBookRecommendationJobCommand, ErrorOr<RetryBookRecommendationJobResult>>
{
    public async Task<ErrorOr<RetryBookRecommendationJobResult>> Handle(
        RetryBookRecommendationJobCommand request,
        CancellationToken cancellationToken)
    {
        var now = dateTimeProvider.UtcNow;
        if (now.Kind != DateTimeKind.Utc)
        {
            return Error.Validation(
                "Recommendation.InvalidClock",
                "L’horloge de l’administration doit être en UTC.");
        }

        if (!Isbn13.TryCreate(request.Isbn13, out var isbn13))
        {
            return Errors.Book.InvalidIsbn(request.Isbn13);
        }

        var bookExists = await dbContext.Books
            .AnyAsync(book => book.Id == isbn13, cancellationToken);
        if (!bookExists)
        {
            return Errors.Book.NotFound(isbn13.Value);
        }

        var job = await dbContext.BookRecommendationJobs
            .SingleOrDefaultAsync(candidate => candidate.Isbn13 == isbn13.Value, cancellationToken);
        if (job is null)
        {
            job = BookRecommendationJob.Create(isbn13.Value);
            dbContext.BookRecommendationJobs.Add(job);
        }

        if (job.Status == BookRecommendationJobStatus.InProgress &&
            (job.StartedAt is null || job.StartedAt > now.AddHours(-1)))
        {
            return Error.Conflict(
                "Recommendation.AlreadyInProgress",
                "Les recommandations de ce livre sont en cours depuis moins d’une heure.");
        }

        if (!job.Request(now, force: true))
        {
            return Error.Conflict(
                "Recommendation.CannotRetry",
                "Ce livre ne peut pas être relancé dans son état actuel.");
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        await workQueue.EnqueueBooksAsync(
            [new BookRecommendationQueueItem(job.Isbn13, job.RequestVersion)],
            cancellationToken);
        return new RetryBookRecommendationJobResult(
            job.Isbn13,
            job.Status.ToString(),
            job.RequestVersion);
    }
}
