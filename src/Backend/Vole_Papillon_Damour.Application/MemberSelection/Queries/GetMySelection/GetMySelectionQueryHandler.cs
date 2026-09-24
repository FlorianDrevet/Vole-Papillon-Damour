using ErrorOr;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Vole_Papillon_Damour.Application.Books.Common;
using Vole_Papillon_Damour.Application.Common.Interfaces.Persistence;
using Vole_Papillon_Damour.Application.Common.Interfaces.Services;
using Vole_Papillon_Damour.Application.Common.Services;
using Vole_Papillon_Damour.Application.MemberSelection.Common;
using Vole_Papillon_Damour.Domain.BookAggregate;
using Vole_Papillon_Damour.Domain.BookAggregate.Entities;
using Vole_Papillon_Damour.Domain.BookAggregate.ValueObjects;
using Vole_Papillon_Damour.Domain.MemberSelectionAggregate;
using Vole_Papillon_Damour.Domain.NotFoundReportAggregate;
using Vole_Papillon_Damour.Domain.NotFoundReportAggregate.ValueObjects;
using Vole_Papillon_Damour.Domain.RareBookAggregate;

namespace Vole_Papillon_Damour.Application.MemberSelection.Queries.GetMySelection;

public sealed class GetMySelectionQueryHandler(
    IProjectDbContext dbContext,
    MemberIdentityService memberIdentityService,
    IDateTimeProvider dateTimeProvider)
    : IRequestHandler<GetMySelectionQuery, ErrorOr<MySelectionResult>>
{
    public async Task<ErrorOr<MySelectionResult>> Handle(
        GetMySelectionQuery query,
        CancellationToken cancellationToken)
    {
        var user = await memberIdentityService.EnsureAsync(
            query.ExternalId,
            query.Email,
            query.FirstName,
            query.LastName,
            cancellationToken);
        var generatedAtUtc = dateTimeProvider.UtcNow;
        if (generatedAtUtc.Kind != DateTimeKind.Utc)
        {
            return Error.Validation(
                "MemberSelection.InvalidClock",
                "The selection clock must be expressed in UTC.");
        }

        var generatedAt = new DateTimeOffset(generatedAtUtc, TimeSpan.Zero);
        var selectionItems = await dbContext.MemberSelectionItems
            .AsNoTracking()
            .Where(item => item.UserId == user.Id)
            .OrderByDescending(item => item.AddedAt)
            .ThenBy(item => item.Id)
            .ToListAsync(cancellationToken);

        var editionIsbns = selectionItems
            .Where(item => item.Isbn13 is not null)
            .Select(item => item.Isbn13!.Value)
            .ToHashSet();
        var rareBookIds = selectionItems
            .Where(item => item.RareBookId is not null)
            .Select(item => item.RareBookId!)
            .ToHashSet();

        List<Book> books = [];
        if (editionIsbns.Count > 0)
        {
            books = await dbContext.Books
                .AsNoTracking()
                .Where(book => editionIsbns.Contains(book.Id))
                .ToListAsync(cancellationToken);
        }

        List<Isbn13> announcedIsbns = [];
        if (editionIsbns.Count > 0)
        {
            announcedIsbns = await dbContext.BookAnnouncements
                .AsNoTracking()
                .Where(announcement =>
                    editionIsbns.Contains(announcement.Isbn13) &&
                    announcement.Status == BookAnnouncementStatus.Announced)
                .Select(announcement => announcement.Isbn13)
                .Distinct()
                .ToListAsync(cancellationToken);
        }

        List<RareBook> rareBooks = [];
        if (rareBookIds.Count > 0)
        {
            rareBooks = await dbContext.RareBooks
                .AsNoTracking()
                .Include(rareBook => rareBook.Photos)
                .Where(rareBook => rareBookIds.Contains(rareBook.Id))
                .ToListAsync(cancellationToken);
        }

        var reportEditionIsbns = selectionItems
            .Where(item => item.Isbn13 is not null)
            .Select(item =>
            {
                var selectedIsbn = item.Isbn13!.Value;
                return books.SingleOrDefault(book => book.Id == selectedIsbn)?.RedirectedToIsbn13 ?? selectedIsbn;
            })
            .ToHashSet();

        List<BookNotFoundReport> notFoundReports = [];
        if (reportEditionIsbns.Count > 0 || rareBookIds.Count > 0)
        {
            notFoundReports = await dbContext.BookNotFoundReports
                .AsNoTracking()
                .Where(report => report.UserId == user.Id &&
                    ((report.Isbn13 != null && reportEditionIsbns.Contains(report.Isbn13.Value)) ||
                     (report.RareBookId != null && rareBookIds.Contains(report.RareBookId))))
                .OrderByDescending(report => report.ReportedAt)
                .ThenByDescending(report => report.Id)
                .ToListAsync(cancellationToken);
        }

        var notFoundReportsByEdition = notFoundReports
            .Where(report => report.Isbn13 is not null)
            .GroupBy(report => report.Isbn13!.Value)
            .ToDictionary(
                group => group.Key,
                group => ProjectNotFoundReport(group.First(), generatedAt));
        var notFoundReportsByRareBook = notFoundReports
            .Where(report => report.RareBookId is not null)
            .GroupBy(report => report.RareBookId!)
            .ToDictionary(
                group => group.Key,
                group => ProjectNotFoundReport(group.First(), generatedAt));

        var activeBookFairs = await dbContext.AssoEvents
            .AsNoTracking()
            .WhereActiveBookFair()
            .ToListAsync(cancellationToken);
        var nextBookFair = BookFairQueries.FindNextBookFair(activeBookFairs, generatedAt);
        var announcedIsbnSet = announcedIsbns.ToHashSet();

        var resultItems = selectionItems
            .Select(item => ProjectItem(
                item,
                books,
                announcedIsbnSet,
                rareBooks,
                notFoundReportsByEdition,
                notFoundReportsByRareBook,
                generatedAt))
            .ToArray();
        var nextFairSummary = nextBookFair is null
            ? null
            : new NextFairSummary(nextBookFair.Id.Value, nextBookFair.DateStart);

        return new MySelectionResult(generatedAt, nextFairSummary, resultItems);
    }

    private static SelectionItemResult ProjectItem(
        MemberSelectionItem item,
        IReadOnlyCollection<Book> books,
        IReadOnlySet<Isbn13> announcedIsbns,
        IReadOnlyCollection<RareBook> rareBooks,
        IReadOnlyDictionary<Isbn13, NotFoundReportSummary?> notFoundReportsByEdition,
        IReadOnlyDictionary<Domain.RareBookAggregate.ValueObjects.RareBookId, NotFoundReportSummary?> notFoundReportsByRareBook,
        DateTimeOffset generatedAt)
    {
        if (item.Isbn13 is { } editionIsbn)
        {
            var book = books.SingleOrDefault(candidate => candidate.Id == editionIsbn);
            var availability = SelectionAvailabilityProjector.ForEdition(
                book,
                announcedIsbns.Contains(editionIsbn));
            var reportIsbn = book?.RedirectedToIsbn13 ?? editionIsbn;
            return new SelectionItemResult(
                item.Id,
                "edition",
                editionIsbn.Value,
                null,
                null,
                book?.Title ?? $"ISBN {editionIsbn.Value}",
                book?.Authors,
                book?.Publisher,
                book?.PublicationYear,
                book?.PhysicalFormat,
                book?.CoverUrl,
                availability,
                generatedAt,
                item.Status,
                new DateTimeOffset(item.AddedAt, TimeSpan.Zero),
                item.PurchasedAt is { } editionPurchasedAt
                    ? new DateTimeOffset(editionPurchasedAt, TimeSpan.Zero)
                    : null,
                notFoundReportsByEdition.GetValueOrDefault(reportIsbn));
        }

        var rareBook = item.RareBookId is { } rareBookId
            ? rareBooks.SingleOrDefault(candidate => candidate.Id == rareBookId)
            : null;
        return new SelectionItemResult(
            item.Id,
            "rare",
            rareBook?.Isbn13?.Value,
            item.RareBookId?.Value,
            rareBook?.Slug.Value,
            rareBook?.Title ?? "Fiche rare indisponible",
            rareBook?.AuthorMention,
            rareBook?.Publisher,
            rareBook?.PublicationYear,
            null,
            rareBook?.Photos.OrderBy(photo => photo.Position).FirstOrDefault()?.BlobUri.ToString(),
            SelectionAvailabilityProjector.ForRareBook(rareBook),
            generatedAt,
            item.Status,
            new DateTimeOffset(item.AddedAt, TimeSpan.Zero),
            item.PurchasedAt is { } rarePurchasedAt
                ? new DateTimeOffset(rarePurchasedAt, TimeSpan.Zero)
                : null,
            item.RareBookId is { } selectedRareBookId
                ? notFoundReportsByRareBook.GetValueOrDefault(selectedRareBookId)
                : null);
    }

    private static NotFoundReportSummary? ProjectNotFoundReport(
        BookNotFoundReport report,
        DateTimeOffset generatedAt)
    {
        if (report.Status == NotFoundReportStatus.Cancelled)
        {
            return null;
        }

        if (report.Status != NotFoundReportStatus.Open &&
            (report.ClosedAt is not { } closedAt || closedAt <= generatedAt.UtcDateTime.AddDays(-30)))
        {
            return null;
        }

        return new NotFoundReportSummary(
            report.Id,
            report.Status.ToString(),
            new DateTimeOffset(report.ReportedAt, TimeSpan.Zero),
            report.ClosedAt is { } closed
                ? new DateTimeOffset(closed, TimeSpan.Zero)
                : null);
    }
}
