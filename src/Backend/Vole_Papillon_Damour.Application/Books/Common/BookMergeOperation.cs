using ErrorOr;
using Microsoft.EntityFrameworkCore;
using Vole_Papillon_Damour.Application.Common.Interfaces.Persistence;
using Vole_Papillon_Damour.Application.NotFoundReports.Common;
using Vole_Papillon_Damour.Domain.BookAggregate;
using Vole_Papillon_Damour.Domain.BookAggregate.ValueObjects;
using Vole_Papillon_Damour.Domain.BookMovementAggregate;
using Vole_Papillon_Damour.Domain.BookMovementAggregate.ValueObjects;
using Vole_Papillon_Damour.Domain.Common.Errors;
using Vole_Papillon_Damour.Domain.NotFoundReportAggregate.ValueObjects;
using Vole_Papillon_Damour.Domain.UserAggregate.ValueObjects;
using Vole_Papillon_Damour.Domain.WatchlistAggregate.ValueObjects;

namespace Vole_Papillon_Damour.Application.Books.Common;

// Fusion d'une fiche source (code interne ou ISBN) vers une fiche cible ISBN. Partagée par la fusion admin
// et l'ajout d'un ISBN à une fiche sans ISBN (RG-174) : même stock transféré, mêmes mouvements de correction,
// même redirection, mêmes suivis repointés (watchlist, signalements « introuvable »).
// N'enregistre rien : l'appelant ouvre la transaction, enregistre et valide.
internal static class BookMergeOperation
{
    public static async Task<ErrorOr<Success>> ApplyAsync(
        IProjectDbContext dbContext,
        INotFoundReportLapser notFoundReportLapser,
        Book source,
        Book target,
        string note,
        UserId updatedBy,
        DateTime mergedAt,
        CancellationToken cancellationToken)
    {
        if (!target.Code.TryGetIsbn13(out var targetIsbn))
        {
            return Errors.Book.InvalidIsbn(target.Code.Value);
        }

        if (source.RedirectedToIsbn13 is not null || target.RedirectedToIsbn13 is not null)
        {
            return Errors.Book.CannotMergeBook("A redirected fiche cannot be used in another merge.");
        }

        BookCode sourceIsbn = source.Code;
        var activeAnnouncements = await dbContext.BookAnnouncements.AnyAsync(
            announcement =>
                announcement.Isbn13 == sourceIsbn &&
                announcement.Status == BookAnnouncementStatus.Announced,
            cancellationToken);
        if (activeAnnouncements)
        {
            return Errors.Book.CannotMergeBook(
                "The source fiche has active announcements; correct or release them before merging.");
        }

        var sourceQuantity = source.QuantityAvailable;
        if (sourceQuantity > 0)
        {
            source.ApplyQuantityCorrection(0, mergedAt);
            dbContext.BookMovements.Add(BookMovement.Create(
                BookMovementId.CreateUnique(),
                sourceIsbn,
                BookMovementType.Correction,
                -sourceQuantity,
                mergedAt,
                mergedAt,
                clockSuspect: false,
                scanSessionId: null,
                updatedBy,
                assoEventsId: null,
                $"Merge.Source: {note}",
                clientGestureId: null));

            target.RecordAvailableEntry(mergedAt, sourceQuantity);
            dbContext.BookMovements.Add(BookMovement.Create(
                BookMovementId.CreateUnique(),
                targetIsbn,
                BookMovementType.Correction,
                sourceQuantity,
                mergedAt,
                mergedAt,
                clockSuspect: false,
                scanSessionId: null,
                updatedBy,
                assoEventsId: null,
                $"Merge.Target: {note}",
                clientGestureId: null));
        }

        source.RedirectTo(targetIsbn);

        var sourceWatchlistItems = await dbContext.WatchlistItems
            .Where(item => item.Scope == WatchlistItemScope.Edition && item.Isbn13 == sourceIsbn)
            .ToListAsync(cancellationToken);
        var targetWatchlistItems = await dbContext.WatchlistItems
            .Where(item => item.Scope == WatchlistItemScope.Edition && item.Isbn13 == targetIsbn)
            .ToListAsync(cancellationToken);
        foreach (var item in sourceWatchlistItems)
        {
            if (targetWatchlistItems.Any(targetItem => targetItem.UserId == item.UserId))
            {
                dbContext.WatchlistItems.Remove(item);
            }
            else
            {
                item.RedirectEdition(targetIsbn);
            }
        }

        var sourceReports = await dbContext.BookNotFoundReports
            .Where(report => report.Isbn13 == sourceIsbn && report.Status == NotFoundReportStatus.Open)
            .ToListAsync(cancellationToken);
        var targetReports = await dbContext.BookNotFoundReports
            .Where(report => report.Isbn13 == targetIsbn && report.Status == NotFoundReportStatus.Open)
            .ToListAsync(cancellationToken);
        foreach (var report in sourceReports)
        {
            var alreadyReportedByMember = report.UserId is { } userId &&
                targetReports.Any(targetReport => targetReport.UserId == userId);
            if (alreadyReportedByMember)
            {
                // The filtered unique index permits one open report per member and
                // canonical edition. Keep the canonical report and retire its duplicate.
                report.Lapse(mergedAt);
                continue;
            }

            report.RetargetEdition(targetIsbn);
            targetReports.Add(report);
        }

        await notFoundReportLapser.LapseIfUnavailableAsync(targetIsbn, mergedAt, cancellationToken);
        return Result.Success;
    }
}
