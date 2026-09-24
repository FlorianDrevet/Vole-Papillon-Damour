using ErrorOr;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Vole_Papillon_Damour.Application.Common.Interfaces.Persistence;
using Vole_Papillon_Damour.Application.Common.Interfaces.Services;
using Vole_Papillon_Damour.Application.NotFoundReports.Queries.GetNotFoundReportQueue;
using Vole_Papillon_Damour.Domain.BookAggregate.ValueObjects;
using Vole_Papillon_Damour.Domain.NotFoundReportAggregate.ValueObjects;
using Vole_Papillon_Damour.Domain.RareBookAggregate.ValueObjects;

namespace Vole_Papillon_Damour.Application.NotFoundReports.Queries.GetNotFoundReportSummary;

public sealed class GetNotFoundReportSummaryQueryHandler(
    IProjectDbContext dbContext,
    IDateTimeProvider dateTimeProvider)
    : IRequestHandler<GetNotFoundReportSummaryQuery, ErrorOr<NotFoundReportSummaryResult>>
{
    public async Task<ErrorOr<NotFoundReportSummaryResult>> Handle(
        GetNotFoundReportSummaryQuery query,
        CancellationToken cancellationToken)
    {
        var hasIsbn = !string.IsNullOrWhiteSpace(query.Isbn13);
        var hasRareBookId = query.RareBookId.HasValue;
        if (hasIsbn && hasRareBookId)
        {
            return Error.Validation(
                "NotFoundReport.InvalidTarget",
                "A summary can target either an edition or a rare book.");
        }

        if (hasRareBookId && query.RareBookId == Guid.Empty)
        {
            return Error.Validation("NotFoundReport.InvalidTarget", "The rare book identifier is invalid.");
        }

        Isbn13 parsedIsbn = default;
        if (hasIsbn && !Isbn13.TryCreate(query.Isbn13, out parsedIsbn))
        {
            return Error.Validation("NotFoundReport.InvalidTarget", "The ISBN-13 is invalid.");
        }

        var openReports = dbContext.BookNotFoundReports.AsNoTracking()
            .Where(report => report.Status == NotFoundReportStatus.Open);
        if (hasIsbn)
        {
            if (query.RareOnly)
            {
                return EmptyTargetSummary();
            }

            return await ForEditionAsync(openReports, parsedIsbn, cancellationToken);
        }

        if (hasRareBookId)
        {
            return await ForRareBookAsync(
                openReports,
                RareBookId.Create(query.RareBookId!.Value),
                cancellationToken);
        }

        var overdueBefore = dateTimeProvider.UtcNow.AddDays(-7);
        var editionFirstReports = openReports
            .Where(report => report.Isbn13 != null)
            .GroupBy(report => report.Isbn13)
            .Select(group => group.Min(report => report.ReportedAt));
        var rareFirstReports = openReports
            .Where(report => report.RareBookId != null)
            .GroupBy(report => report.RareBookId)
            .Select(group => group.Min(report => report.ReportedAt));

        var editionTargetCount = query.RareOnly ? 0 : await editionFirstReports.CountAsync(cancellationToken);
        var editionOverdueCount = query.RareOnly
            ? 0
            : await editionFirstReports.CountAsync(
                reportedAt => reportedAt < overdueBefore,
                cancellationToken);
        var rareTargetCount = await rareFirstReports.CountAsync(cancellationToken);
        var rareOverdueCount = await rareFirstReports.CountAsync(
            reportedAt => reportedAt < overdueBefore,
            cancellationToken);

        return new NotFoundReportSummaryResult(
            editionTargetCount + rareTargetCount,
            editionOverdueCount + rareOverdueCount,
            OpenReportCount: 0,
            FirstReportedAt: null,
            LatestComment: null);
    }

    private async Task<NotFoundReportSummaryResult> ForEditionAsync(
        IQueryable<Domain.NotFoundReportAggregate.BookNotFoundReport> openReports,
        Isbn13 isbn13,
        CancellationToken cancellationToken)
    {
        var targetReports = openReports.Where(report => report.Isbn13 == isbn13);
        var count = await targetReports.CountAsync(cancellationToken);
        if (count == 0)
        {
            return EmptyTargetSummary();
        }

        var firstReportedAt = await targetReports.MinAsync(report => report.ReportedAt, cancellationToken);
        var latestComment = await LatestCommentAsync(targetReports, cancellationToken);
        return new NotFoundReportSummaryResult(
            0,
            0,
            count,
            ToOffset(firstReportedAt),
            latestComment);
    }

    private async Task<NotFoundReportSummaryResult> ForRareBookAsync(
        IQueryable<Domain.NotFoundReportAggregate.BookNotFoundReport> openReports,
        RareBookId rareBookId,
        CancellationToken cancellationToken)
    {
        var targetReports = openReports.Where(report => report.RareBookId == rareBookId);
        var count = await targetReports.CountAsync(cancellationToken);
        if (count == 0)
        {
            return EmptyTargetSummary();
        }

        var firstReportedAt = await targetReports.MinAsync(report => report.ReportedAt, cancellationToken);
        var latestComment = await LatestCommentAsync(targetReports, cancellationToken);
        return new NotFoundReportSummaryResult(
            0,
            0,
            count,
            ToOffset(firstReportedAt),
            latestComment);
    }

    private static async Task<NotFoundReportCommentResult?> LatestCommentAsync(
        IQueryable<Domain.NotFoundReportAggregate.BookNotFoundReport> targetReports,
        CancellationToken cancellationToken)
    {
        var comment = await targetReports
            .Where(report => report.Comment != null)
            .OrderByDescending(report => report.ReportedAt)
            .Select(report => new LatestCommentProjection
            {
                Text = report.Comment!,
                Location = report.Location,
                ReportedAt = report.ReportedAt
            })
            .FirstOrDefaultAsync(cancellationToken);
        return comment is null
            ? null
            : new NotFoundReportCommentResult(
                comment.Text,
                comment.Location?.ToString(),
                ToOffset(comment.ReportedAt));
    }

    private static NotFoundReportSummaryResult EmptyTargetSummary() =>
        new(0, 0, 0, null, null);

    private static DateTimeOffset ToOffset(DateTime value) =>
        new(DateTime.SpecifyKind(value, DateTimeKind.Utc), TimeSpan.Zero);

    private sealed class LatestCommentProjection
    {
        public string Text { get; init; } = string.Empty;
        public NotFoundReportLocation? Location { get; init; }
        public DateTime ReportedAt { get; init; }
    }
}
