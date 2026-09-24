using ErrorOr;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Vole_Papillon_Damour.Application.Common.Interfaces.Persistence;
using Vole_Papillon_Damour.Application.Common.Interfaces.Services;
using Vole_Papillon_Damour.Domain.BookAggregate;
using Vole_Papillon_Damour.Domain.BookAggregate.ValueObjects;
using Vole_Papillon_Damour.Domain.NotFoundReportAggregate.ValueObjects;
using Vole_Papillon_Damour.Domain.RareBookAggregate;
using Vole_Papillon_Damour.Domain.RareBookAggregate.ValueObjects;
using Vole_Papillon_Damour.Domain.UserAggregate.ValueObjects;

namespace Vole_Papillon_Damour.Application.NotFoundReports.Queries.GetClosedNotFoundReports;

public sealed class GetClosedNotFoundReportsQueryHandler(
    IProjectDbContext dbContext,
    IDateTimeProvider dateTimeProvider)
    : IRequestHandler<GetClosedNotFoundReportsQuery, ErrorOr<ClosedNotFoundReportsResult>>
{
    public async Task<ErrorOr<ClosedNotFoundReportsResult>> Handle(
        GetClosedNotFoundReportsQuery query,
        CancellationToken cancellationToken)
    {
        if (query.Page < 1 || query.PageSize is < 1 or > 50)
        {
            return Error.Validation("NotFoundReport.InvalidPage", "The closed report page is invalid.");
        }

        var closedReports = dbContext.BookNotFoundReports.AsNoTracking()
            .Where(report => report.Status == NotFoundReportStatus.Found ||
                             report.Status == NotFoundReportStatus.Withdrawn ||
                             report.Status == NotFoundReportStatus.Dismissed ||
                             report.Status == NotFoundReportStatus.Lapsed);

        var editionClosures = closedReports
            .Where(report => report.Isbn13 != null)
            .GroupBy(report => new { report.Isbn13, report.ClosedAt, report.ClosedBy, report.Status })
            .Select(reports => new ClosedProjection
            {
                Kind = "edition",
                Isbn13 = reports.Key.Isbn13,
                RareBookId = null,
                ClosedAt = reports.Key.ClosedAt,
                ClosedBy = reports.Key.ClosedBy,
                Status = reports.Key.Status,
                Title = string.Empty,
                ReportCount = reports.Count(),
                WithdrawnQuantity = reports.Max(report => report.WithdrawnQuantity),
                Note = reports.Max(report => report.ClosureNote)
            });

        var rareClosures = closedReports
            .Where(report => report.RareBookId != null)
            .GroupBy(report => new { report.RareBookId, report.ClosedAt, report.ClosedBy, report.Status })
            .Select(reports => new ClosedProjection
            {
                Kind = "rare",
                Isbn13 = null,
                RareBookId = reports.Key.RareBookId,
                ClosedAt = reports.Key.ClosedAt,
                ClosedBy = reports.Key.ClosedBy,
                Status = reports.Key.Status,
                Title = string.Empty,
                ReportCount = reports.Count(),
                WithdrawnQuantity = reports.Max(report => report.WithdrawnQuantity),
                Note = reports.Max(report => report.ClosureNote)
            });

        if (query.Page > int.MaxValue / query.PageSize)
        {
            return Error.Validation("NotFoundReport.InvalidPage", "The closed report page is invalid.");
        }

        var editionCount = query.RareOnly ? 0 : await editionClosures.CountAsync(cancellationToken);
        var rareCount = await rareClosures.CountAsync(cancellationToken);
        var totalCount = editionCount + rareCount;
        var candidateCount = (query.Page - 1) * query.PageSize + query.PageSize;
        var editionCandidates = query.RareOnly
            ? []
            : await editionClosures.OrderByDescending(closure => closure.ClosedAt)
                .Take(candidateCount).ToListAsync(cancellationToken);
        var rareCandidates = await rareClosures.OrderByDescending(closure => closure.ClosedAt)
            .Take(candidateCount).ToListAsync(cancellationToken);
        var pageClosures = editionCandidates.Concat(rareCandidates)
            .OrderByDescending(closure => closure.ClosedAt)
            .ThenBy(closure => closure.Kind)
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .ToList();

        var editionIds = pageClosures.Where(closure => closure.Isbn13.HasValue)
            .Select(closure => closure.Isbn13!.Value)
            .Distinct()
            .ToArray();
        List<Book> books = editionIds.Length == 0
            ? []
            : await dbContext.Books.AsNoTracking()
                .Where(book => editionIds.Contains(book.Id))
                .ToListAsync(cancellationToken);
        var booksByIsbn = books.ToDictionary(book => book.Id.Value);

        var rareBookIds = pageClosures.Where(closure => closure.RareBookId is not null)
            .Select(closure => closure.RareBookId!)
            .Distinct()
            .ToArray();
        List<RareBook> rareBooks = rareBookIds.Length == 0
            ? []
            : await dbContext.RareBooks.AsNoTracking()
                .Where(book => rareBookIds.Contains(book.Id))
                .ToListAsync(cancellationToken);
        var rareBooksById = rareBooks.ToDictionary(book => book.Id.Value);

        var volunteerIds = pageClosures.Where(closure => closure.ClosedBy != null)
            .Select(closure => closure.ClosedBy!)
            .Distinct()
            .ToArray();
        var volunteerNames = volunteerIds.Length == 0
            ? []
            : await dbContext.Users.AsNoTracking()
                .Where(user => volunteerIds.Contains(user.Id))
                .Select(user => new VolunteerNameProjection
                {
                    Id = user.Id,
                    FirstName = user.Name == null ? null : user.Name.FirstName,
                    LastName = user.Name == null ? null : user.Name.LastName
                })
                .ToListAsync(cancellationToken);
        var nameLookup = volunteerNames.ToDictionary(
            volunteer => volunteer.Id.Value,
            volunteer => string.Join(" ", new[] { volunteer.FirstName, volunteer.LastName }
                .Where(part => !string.IsNullOrWhiteSpace(part))));

        var items = pageClosures.Select(closure =>
        {
            Book? book = closure.Isbn13 is { } isbn ? booksByIsbn.GetValueOrDefault(isbn.Value) : null;
            RareBook? rareBook = closure.RareBookId is { } rareId
                ? rareBooksById.GetValueOrDefault(rareId.Value)
                : null;

            return new ClosedNotFoundReportResult(
                ToOffset(closure.ClosedAt!.Value),
                closure.Kind,
                closure.Isbn13?.Value,
                closure.RareBookId?.Value,
                book?.Title ?? rareBook?.Title ?? string.Empty,
                closure.Status.ToString(),
                closure.ReportCount,
                closure.WithdrawnQuantity,
                closure.ClosedBy is { } closedBy ? nameLookup.GetValueOrDefault(closedBy.Value) : null,
                closure.Note);
        }).ToArray();

        return new ClosedNotFoundReportsResult(
            ToOffset(dateTimeProvider.UtcNow),
            query.Page,
            query.PageSize,
            totalCount,
            items);
    }

    private static DateTimeOffset ToOffset(DateTime value) =>
        new(DateTime.SpecifyKind(value, DateTimeKind.Utc), TimeSpan.Zero);

    private sealed class ClosedProjection
    {
        public string Kind { get; init; } = string.Empty;
        public Isbn13? Isbn13 { get; init; }
        public RareBookId? RareBookId { get; init; }
        public DateTime? ClosedAt { get; init; }
        public UserId? ClosedBy { get; init; }
        public NotFoundReportStatus Status { get; init; }
        public string Title { get; init; } = string.Empty;
        public int ReportCount { get; init; }
        public int? WithdrawnQuantity { get; init; }
        public string? Note { get; init; }
    }

    private sealed class VolunteerNameProjection
    {
        public UserId Id { get; init; } = null!;
        public string? FirstName { get; init; }
        public string? LastName { get; init; }
    }
}
