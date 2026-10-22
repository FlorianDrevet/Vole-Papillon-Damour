using ErrorOr;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Vole_Papillon_Damour.Application.Common.Interfaces.Persistence;
using Vole_Papillon_Damour.Application.Common.Interfaces.Services;
using Vole_Papillon_Damour.Domain.RecommendationAggregate;

namespace Vole_Papillon_Damour.Application.Books.Queries.Admin;

public sealed class GetAdminBookRecommendationsQueryHandler(
    IProjectDbContext dbContext,
    IDateTimeProvider dateTimeProvider)
    : IRequestHandler<GetAdminBookRecommendationsQuery, ErrorOr<AdminBookRecommendationPageResult>>
{
    private const int MaximumPageSize = 100;

    public async Task<ErrorOr<AdminBookRecommendationPageResult>> Handle(
        GetAdminBookRecommendationsQuery query,
        CancellationToken cancellationToken)
    {
        if (query.Page <= 0 || query.PageSize is <= 0 or > MaximumPageSize)
        {
            return Error.Validation(
                "Recommendation.InvalidAdminPage",
                $"Page must be positive and page size must be between 1 and {MaximumPageSize}.");
        }

        var generatedAt = dateTimeProvider.UtcNow;
        if (generatedAt.Kind != DateTimeKind.Utc)
        {
            return Error.Validation(
                "Recommendation.InvalidClock",
                "The administration clock must be expressed in UTC.");
        }

        var joined =
            from book in dbContext.Books.AsNoTracking()
            where !book.IsHiddenFromCatalog && book.RedirectedToIsbn13 == null
            join job in dbContext.BookRecommendationJobs.AsNoTracking()
                on book.Id.Value equals job.Isbn13 into recommendationJobs
            from job in recommendationJobs.DefaultIfEmpty()
            select new { Book = book, Job = job };

        if (query.Status is { } status)
        {
            joined = status == BookRecommendationJobStatus.Pending
                ? joined.Where(item => item.Job == null || item.Job.Status == status)
                : joined.Where(item => item.Job != null && item.Job.Status == status);
        }

        var search = query.Search?.Trim();
        if (!string.IsNullOrWhiteSpace(search))
        {
            var pattern = $"%{search.Replace("%", "[%]", StringComparison.Ordinal).Replace("_", "[_]", StringComparison.Ordinal)}%";
            joined = joined.Where(item =>
                EF.Functions.Like(item.Book.Id.Value, pattern) ||
                EF.Functions.Like(item.Book.Title ?? string.Empty, pattern) ||
                EF.Functions.Like(item.Book.Authors ?? string.Empty, pattern));
        }

        var totalCount = await joined.CountAsync(cancellationToken);
        var pageItems = await joined
            .OrderBy(item => item.Job == null ? 0 : 1)
            .ThenBy(item => item.Job == null ? DateTime.MinValue : item.Job.RequestedAt)
            .ThenBy(item => item.Book.Id.Value)
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .Select(item => new
            {
                Isbn13 = item.Book.Id.Value,
                item.Book.Title,
                item.Book.Authors,
                Status = item.Job == null ? BookRecommendationJobStatus.Pending : item.Job.Status,
                Stage = item.Job == null ? BookRecommendationJobStage.Notice : item.Job.Stage,
                AttemptCount = item.Job == null ? 0 : item.Job.AttemptCount,
                RequestedAt = item.Job == null ? (DateTime?)null : item.Job.RequestedAt,
                StartedAt = item.Job == null ? null : item.Job.StartedAt,
                CompletedAt = item.Job == null ? null : item.Job.CompletedAt,
                LastError = item.Job == null ? null : item.Job.LastError,
            })
            .ToListAsync(cancellationToken);

        return new AdminBookRecommendationPageResult(
            new DateTimeOffset(generatedAt, TimeSpan.Zero),
            pageItems.Select(item => new AdminBookRecommendationItemResult(
                item.Isbn13,
                item.Title,
                item.Authors,
                item.Status,
                item.Stage,
                item.AttemptCount,
                item.RequestedAt is { } requestedAt ? new DateTimeOffset(requestedAt, TimeSpan.Zero) : null,
                item.StartedAt is { } startedAt ? new DateTimeOffset(startedAt, TimeSpan.Zero) : null,
                item.CompletedAt is { } completedAt ? new DateTimeOffset(completedAt, TimeSpan.Zero) : null,
                item.LastError)).ToArray(),
            totalCount,
            query.Page,
            query.PageSize);
    }
}
