using Microsoft.EntityFrameworkCore;
using Vole_Papillon_Damour.Application.Common.Interfaces.Persistence;
using Vole_Papillon_Damour.Domain.BookAggregate.ValueObjects;
using Vole_Papillon_Damour.Domain.NotFoundReportAggregate.ValueObjects;
using Vole_Papillon_Damour.Domain.RareBookAggregate.ValueObjects;

namespace Vole_Papillon_Damour.Application.NotFoundReports.Common;

public sealed class NotFoundReportLapser(IProjectDbContext dbContext) : INotFoundReportLapser
{
    public async Task LapseIfUnavailableAsync(Isbn13 isbn13, DateTime at, CancellationToken ct)
    {
        var book = await dbContext.Books
            .SingleOrDefaultAsync(candidate => candidate.Id == isbn13, ct);
        if (book?.RedirectedToIsbn13 is { } canonicalIsbn13)
        {
            isbn13 = canonicalIsbn13;
            book = await dbContext.Books
                .SingleOrDefaultAsync(candidate => candidate.Id == canonicalIsbn13, ct);
        }

        if (book is null || book.QuantityAvailable > 0)
        {
            return;
        }

        var reports = await dbContext.BookNotFoundReports
            .Where(report => report.Isbn13 == isbn13 && report.Status == NotFoundReportStatus.Open)
            .ToListAsync(ct);
        var trackedReports = dbContext.BookNotFoundReports.Local
            .Where(report => report.Isbn13 == isbn13 && report.IsOpen);
        foreach (var report in reports.Concat(trackedReports).DistinctBy(report => report.Id))
        {
            if (report.IsOpen)
            {
                report.Lapse(at);
            }
        }
    }

    public async Task LapseRareBookAsync(RareBookId rareBookId, DateTime at, CancellationToken ct)
    {
        var rareBook = await dbContext.RareBooks
            .SingleOrDefaultAsync(candidate => candidate.Id == rareBookId, ct);
        if (rareBook is null ||
            (rareBook.Status == RareBookStatus.Published && !rareBook.IsSold))
        {
            return;
        }

        var reports = await dbContext.BookNotFoundReports
            .Where(report => report.RareBookId == rareBookId && report.Status == NotFoundReportStatus.Open)
            .ToListAsync(ct);
        var trackedReports = dbContext.BookNotFoundReports.Local
            .Where(report => report.RareBookId == rareBookId && report.IsOpen);
        foreach (var report in reports.Concat(trackedReports).DistinctBy(report => report.Id))
        {
            if (report.IsOpen)
            {
                report.Lapse(at);
            }
        }
    }
}
