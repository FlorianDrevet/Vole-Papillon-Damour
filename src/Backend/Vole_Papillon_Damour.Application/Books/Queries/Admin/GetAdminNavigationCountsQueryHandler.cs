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
        var overdueBefore = dateTimeProvider.UtcNow.AddDays(-7);
        var rareReportFirstSeen = openReports
            .Where(report => report.RareBookId != null)
            .GroupBy(report => report.RareBookId)
            .Select(group => group.Min(report => report.ReportedAt));
        var notFoundOpenTargets = await rareReportFirstSeen.CountAsync(cancellationToken);
        var notFoundOverdueTargets = await rareReportFirstSeen.CountAsync(
            reportedAt => reportedAt < overdueBefore,
            cancellationToken);

        if (!query.IsRareBooksOnly)
        {
            var editionReportFirstSeen = openReports
                .Where(report => report.Isbn13 != null)
                .GroupBy(report => report.Isbn13)
                .Select(group => group.Min(report => report.ReportedAt));
            notFoundOpenTargets += await editionReportFirstSeen.CountAsync(cancellationToken);
            notFoundOverdueTargets += await editionReportFirstSeen.CountAsync(
                reportedAt => reportedAt < overdueBefore,
                cancellationToken);
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

        var now = dateTimeProvider.UtcNow;
        var foundRequests = dbContext.ManualBookRequests.AsNoTracking()
            .Where(request => request.Status == ManualBookRequestStatus.Found);
        var manualRequestsFound = await foundRequests.CountAsync(cancellationToken);
        var manualRequestsFoundOverdue = await foundRequests.CountAsync(
            request => request.FoundAt < now.AddDays(-14),
            cancellationToken);
        var openScanSessions = await dbContext.ScanSessions.AsNoTracking()
            .CountAsync(session => session.Status == ScanSessionStatus.InProgress, cancellationToken);
        var catalogueBooks = await AdminQueryPredicates.Catalogue(dbContext.Books.AsNoTracking())
            .CountAsync(cancellationToken);
        var isbnQueue = await AdminQueryPredicates.MetadataQueue(dbContext.Books.AsNoTracking())
            .CountAsync(cancellationToken);
        var isbndbAbsentToComplete = await AdminQueryPredicates
            .IsbndbAbsentToComplete(dbContext.Books.AsNoTracking())
            .CountAsync(cancellationToken);
        var isbnNotFoundCount = await dbContext.Books.AsNoTracking()
            .CountAsync(book => book.MetadataStatus == BookMetadataStatus.NotFound, cancellationToken);
        var isbnFailedCount = await dbContext.Books.AsNoTracking()
            .CountAsync(book => book.MetadataStatus == BookMetadataStatus.Failed, cancellationToken);

        return new AdminNavigationCountsResult(
            openScanSessions,
            notFoundOpenTargets,
            notFoundOverdueTargets,
            manualRequestsFound,
            manualRequestsFoundOverdue,
            catalogueBooks,
            isbnQueue,
            isbndbAbsentToComplete,
            publishedRareBooks,
            isbnNotFoundCount,
            isbnFailedCount);
    }
}
