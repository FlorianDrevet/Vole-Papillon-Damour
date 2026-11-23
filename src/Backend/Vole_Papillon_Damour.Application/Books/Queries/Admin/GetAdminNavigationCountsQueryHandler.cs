using ErrorOr;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Vole_Papillon_Damour.Application.Books.Common;
using Vole_Papillon_Damour.Application.Common.Interfaces.Persistence;
using Vole_Papillon_Damour.Application.Common.Interfaces.Services;
using Vole_Papillon_Damour.Domain.BookAggregate.ValueObjects;
using Vole_Papillon_Damour.Domain.ManualRequestAggregate.ValueObjects;
using Vole_Papillon_Damour.Domain.NotFoundReportAggregate.ValueObjects;
using Vole_Papillon_Damour.Domain.RareBookAggregate.ValueObjects;
using Vole_Papillon_Damour.Domain.ScanSessionAggregate.ValueObjects;

namespace Vole_Papillon_Damour.Application.Books.Queries.Admin;

public sealed class GetAdminNavigationCountsQueryHandler(
    IProjectDbContext dbContext,
    IDateTimeProvider dateTimeProvider)
    : IRequestHandler<GetAdminNavigationCountsQuery, ErrorOr<AdminNavigationCountsResult>>
{
    public async Task<ErrorOr<AdminNavigationCountsResult>> Handle(
        GetAdminNavigationCountsQuery query,
        CancellationToken cancellationToken)
    {
        var openReports = dbContext.BookNotFoundReports.AsNoTracking()
            .Where(report => report.Status == NotFoundReportStatus.Open);
        var now = dateTimeProvider.UtcNow;
        var overdueBefore = now.AddDays(-7);
        var rareReportFirstSeen = openReports
            .Where(report => report.RareBookId != null)
            .GroupBy(report => report.RareBookId)
            .Select(group => group.Min(report => report.ReportedAt));
        var rareReportCounts = await rareReportFirstSeen
            .GroupBy(_ => 1)
            .Select(group => new
            {
                Open = group.Count(),
                Overdue = group.Count(reportedAt => reportedAt < overdueBefore),
            })
            .SingleOrDefaultAsync(cancellationToken);
        var notFoundOpenTargets = rareReportCounts?.Open ?? 0;
        var notFoundOverdueTargets = rareReportCounts?.Overdue ?? 0;

        if (!query.IsRareBooksOnly)
        {
            var editionReportFirstSeen = openReports
                .Where(report => report.Isbn13 != null)
                .GroupBy(report => report.Isbn13)
                .Select(group => group.Min(report => report.ReportedAt));
            var editionReportCounts = await editionReportFirstSeen
                .GroupBy(_ => 1)
                .Select(group => new
                {
                    Open = group.Count(),
                    Overdue = group.Count(reportedAt => reportedAt < overdueBefore),
                })
                .SingleOrDefaultAsync(cancellationToken);
            notFoundOpenTargets += editionReportCounts?.Open ?? 0;
            notFoundOverdueTargets += editionReportCounts?.Overdue ?? 0;
        }

        var publishedRareBooks = await dbContext.RareBooks.AsNoTracking()
            .CountAsync(book => book.Status == RareBookStatus.Published, cancellationToken);
        if (query.IsRareBooksOnly)
        {
            return new AdminNavigationCountsResult(
                null,
                notFoundOpenTargets,
                notFoundOverdueTargets,
                null,
                null,
                null,
                null,
                null,
                publishedRareBooks);
        }

        var foundRequests = dbContext.ManualBookRequests.AsNoTracking()
            .Where(request => request.Status == ManualBookRequestStatus.Found);
        var manualRequestCounts = await foundRequests
            .GroupBy(_ => 1)
            .Select(group => new
            {
                Found = group.Count(),
                Overdue = group.Count(request => request.FoundAt < now.AddDays(-14)),
            })
            .SingleOrDefaultAsync(cancellationToken);
        var openScanSessions = await dbContext.ScanSessions.AsNoTracking()
            .CountAsync(session => session.Status == ScanSessionStatus.InProgress, cancellationToken);
        var bookCounts = await dbContext.Books.AsNoTracking()
            .GroupBy(_ => 1)
            .Select(group => new
            {
                CatalogueBooks = group.Count(book =>
                    (book.MetadataStatus == BookMetadataStatus.Resolved ||
                     book.MetadataStatus == BookMetadataStatus.Manual) &&
                    book.Title != null &&
                    book.Title.Trim() != string.Empty),
                IsbnQueue = group.Count(book =>
                    book.MetadataStatus != BookMetadataStatus.Discarded &&
                    (book.MetadataStatus == BookMetadataStatus.Pending ||
                     book.MetadataStatus == BookMetadataStatus.Failed ||
                     book.MetadataStatus == BookMetadataStatus.NotFound ||
                     book.Title == null ||
                     book.Title.Trim() == string.Empty)),
                IsbndbAbsentToComplete = group.Count(book =>
                    book.IsAbsentFromIsbndb &&
                    book.IsbndbCompletedAt == null &&
                    book.Title != null &&
                    book.Title.Trim() != string.Empty),
                IsbnNotFoundCount = group.Count(book => book.MetadataStatus == BookMetadataStatus.NotFound),
                IsbnFailedCount = group.Count(book => book.MetadataStatus == BookMetadataStatus.Failed),
            })
            .SingleOrDefaultAsync(cancellationToken);

        return new AdminNavigationCountsResult(
            openScanSessions,
            notFoundOpenTargets,
            notFoundOverdueTargets,
            manualRequestCounts?.Found ?? 0,
            manualRequestCounts?.Overdue ?? 0,
            bookCounts?.CatalogueBooks ?? 0,
            bookCounts?.IsbnQueue ?? 0,
            bookCounts?.IsbndbAbsentToComplete ?? 0,
            publishedRareBooks,
            bookCounts?.IsbnNotFoundCount ?? 0,
            bookCounts?.IsbnFailedCount ?? 0);
    }
}
