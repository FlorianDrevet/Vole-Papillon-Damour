using ErrorOr;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Vole_Papillon_Damour.Application.Common.Interfaces.Persistence;
using Vole_Papillon_Damour.Domain.BookAggregate;
using Vole_Papillon_Damour.Domain.BookAggregate.ValueObjects;
using Vole_Papillon_Damour.Domain.Common.Errors;
using Vole_Papillon_Damour.Domain.NotFoundReportAggregate;
using Vole_Papillon_Damour.Domain.NotFoundReportAggregate.ValueObjects;
using Vole_Papillon_Damour.Domain.RareBookAggregate;
using Vole_Papillon_Damour.Domain.RareBookAggregate.ValueObjects;

namespace Vole_Papillon_Damour.Application.NotFoundReports.Common;

internal static class NotFoundReportClosureSupport
{
    public static async Task<ErrorOr<ResolvedNotFoundReportTarget>> LoadAsync(
        IProjectDbContext dbContext,
        NotFoundReportTargetRef target,
        CancellationToken cancellationToken)
    {
        if (string.Equals(target.Kind, "edition", StringComparison.OrdinalIgnoreCase))
        {
            if (!Isbn13.TryCreate(target.Reference, out var requestedIsbn))
            {
                return Errors.NotFoundReport.InvalidTarget();
            }

            var book = await dbContext.Books.SingleOrDefaultAsync(
                candidate => candidate.Id == requestedIsbn,
                cancellationToken);
            if (book is null)
            {
                return Errors.Book.NotFound(requestedIsbn.Value);
            }

            var canonicalIsbn = book.RedirectedToIsbn13 ?? requestedIsbn;
            if (canonicalIsbn != requestedIsbn)
            {
                book = await dbContext.Books.SingleOrDefaultAsync(
                    candidate => candidate.Id == canonicalIsbn,
                    cancellationToken);
                if (book is null)
                {
                    return Errors.Book.NotFound(canonicalIsbn.Value);
                }
            }

            var reports = await dbContext.BookNotFoundReports
                .Where(report => report.Status == NotFoundReportStatus.Open && report.Isbn13 == canonicalIsbn)
                .ToListAsync(cancellationToken);
            if (reports.Count == 0)
            {
                return Errors.NotFoundReport.NothingToClose();
            }

            return new ResolvedNotFoundReportTarget(
                new NotFoundReportTargetRef("edition", canonicalIsbn.Value),
                canonicalIsbn,
                null,
                book,
                null,
                reports);
        }

        if (string.Equals(target.Kind, "rare", StringComparison.OrdinalIgnoreCase) &&
            Guid.TryParse(target.Reference, out var rareBookGuid) &&
            rareBookGuid != Guid.Empty)
        {
            var rareBookId = RareBookId.Create(rareBookGuid);
            var rareBook = await dbContext.RareBooks.SingleOrDefaultAsync(
                candidate => candidate.Id == rareBookId,
                cancellationToken);
            if (rareBook is null)
            {
                return Errors.RareBook.NotFound(rareBookGuid);
            }

            var reports = await dbContext.BookNotFoundReports
                .Where(report => report.Status == NotFoundReportStatus.Open && report.RareBookId == rareBookId)
                .ToListAsync(cancellationToken);
            if (reports.Count == 0)
            {
                return Errors.NotFoundReport.NothingToClose();
            }

            return new ResolvedNotFoundReportTarget(
                new NotFoundReportTargetRef("rare", rareBookGuid.ToString("D")),
                null,
                rareBookId,
                null,
                rareBook,
                reports);
        }

        return Errors.NotFoundReport.InvalidTarget();
    }

    public static void LogClosed(
        ILogger logger,
        ResolvedNotFoundReportTarget target,
        string outcome,
        int count,
        int? withdrawn)
    {
        logger.LogInformation(
            "NotFoundReportsClosed {target} {outcome} {count} {withdrawn}",
            $"{target.Reference.Kind}:{target.Reference.Reference}",
            outcome,
            count,
            withdrawn);
    }
}

internal sealed record ResolvedNotFoundReportTarget(
    NotFoundReportTargetRef Reference,
    Isbn13? Isbn13,
    RareBookId? RareBookId,
    Book? Book,
    RareBook? RareBook,
    IReadOnlyList<BookNotFoundReport> Reports);
