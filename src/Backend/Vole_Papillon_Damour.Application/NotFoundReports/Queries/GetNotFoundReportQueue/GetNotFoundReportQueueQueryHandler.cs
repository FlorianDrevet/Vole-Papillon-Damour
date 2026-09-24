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

namespace Vole_Papillon_Damour.Application.NotFoundReports.Queries.GetNotFoundReportQueue;

public sealed class GetNotFoundReportQueueQueryHandler(
    IProjectDbContext dbContext,
    IDateTimeProvider dateTimeProvider)
    : IRequestHandler<GetNotFoundReportQueueQuery, ErrorOr<NotFoundReportQueueResult>>
{
    public async Task<ErrorOr<NotFoundReportQueueResult>> Handle(
        GetNotFoundReportQueueQuery query,
        CancellationToken cancellationToken)
    {
        var kind = string.IsNullOrWhiteSpace(query.Kind) ? "all" : query.Kind.Trim().ToLowerInvariant();
        var sort = string.IsNullOrWhiteSpace(query.Sort)
            ? "most-reported"
            : query.Sort.Trim().ToLowerInvariant();
        if (kind is not ("all" or "edition" or "rare"))
        {
            return Error.Validation("NotFoundReport.InvalidKind", "The report target kind is not supported.");
        }

        if (sort is not ("most-reported" or "oldest" or "newest" or "genre"))
        {
            return Error.Validation("NotFoundReport.InvalidSort", "The report queue sort is not supported.");
        }

        if (query.Page < 1 || query.PageSize is < 1 or > 50 || query.Page > int.MaxValue / query.PageSize)
        {
            return Error.Validation("NotFoundReport.InvalidPage", "The report queue page is invalid.");
        }

        var includeEditions = !query.RareOnly && kind is "all" or "edition";
        var includeRareBooks = query.RareOnly || kind is "all" or "rare";
        var openReports = dbContext.BookNotFoundReports.AsNoTracking()
            .Where(report => report.Status == NotFoundReportStatus.Open);

        var editionTargets = openReports
            .Where(report => report.Isbn13 != null)
            .GroupBy(report => report.Isbn13)
            .Select(reports => new QueueTargetProjection
            {
                Kind = "edition",
                Isbn13 = reports.Key,
                RareBookId = null,
                Title = string.Empty,
                Authors = null,
                Publisher = null,
                PublicationYear = null,
                CoverUrl = null,
                Genre = dbContext.Books.AsNoTracking()
                    .Where(book => (Isbn13?)book.Id == reports.Key)
                    .Select(book => book.Genre)
                    .FirstOrDefault(),
                QuantityAvailable = 0,
                ReportCount = reports.Count(),
                // The filtered unique index guarantees one open report per member and
                // edition; each anonymized report also contributes one to this count.
                MemberCount = reports.Count(),
                FirstReportedAt = reports.Min(report => report.ReportedAt),
                LastReportedAt = reports.Max(report => report.ReportedAt)
            });

        var rareTargets = openReports
            .Where(report => report.RareBookId != null)
            .GroupBy(report => report.RareBookId)
            .Select(reports => new QueueTargetProjection
            {
                Kind = "rare",
                Isbn13 = null,
                RareBookId = reports.Key,
                Title = string.Empty,
                Authors = null,
                Publisher = null,
                PublicationYear = null,
                CoverUrl = null,
                Genre = null,
                QuantityAvailable = 1,
                ReportCount = reports.Count(),
                MemberCount = reports.Count(),
                FirstReportedAt = reports.Min(report => report.ReportedAt),
                LastReportedAt = reports.Max(report => report.ReportedAt)
            });

        var overdueBefore = dateTimeProvider.UtcNow.AddDays(-7);
        var editionCount = includeEditions
            ? await editionTargets.CountAsync(cancellationToken)
            : 0;
        var rareCount = includeRareBooks
            ? await rareTargets.CountAsync(cancellationToken)
            : 0;
        var overdueEditionCount = includeEditions
            ? await editionTargets.CountAsync(target => target.FirstReportedAt < overdueBefore, cancellationToken)
            : 0;
        var overdueRareCount = includeRareBooks
            ? await rareTargets.CountAsync(target => target.FirstReportedAt < overdueBefore, cancellationToken)
            : 0;
        var totalCount = editionCount + rareCount;
        var overdueCount = overdueEditionCount + overdueRareCount;
        var openReportCount = kind switch
        {
            "edition" when !query.RareOnly => await openReports.CountAsync(
                report => report.Isbn13 != null, cancellationToken),
            "rare" when !query.RareOnly => await openReports.CountAsync(
                report => report.RareBookId != null, cancellationToken),
            _ when query.RareOnly => await openReports.CountAsync(
                report => report.RareBookId != null, cancellationToken),
            _ => await openReports.CountAsync(cancellationToken)
        };

        // Query each grouped target kind separately. EF Core cannot translate a set
        // operation after materializing these value-converted group keys. Fetching the
        // first offset + page rows from each server-ordered query is sufficient to
        // produce the globally ordered page after merging the two bounded lists.
        var candidateCount = (query.Page - 1) * query.PageSize + query.PageSize;
        var editionCandidates = includeEditions
            ? await ApplyOrder(editionTargets, sort).Take(candidateCount).ToListAsync(cancellationToken)
            : [];
        var rareCandidates = includeRareBooks
            ? await ApplyOrder(rareTargets, sort).Take(candidateCount).ToListAsync(cancellationToken)
            : [];
        var pageTargets = OrderInMemory(editionCandidates.Concat(rareCandidates), sort)
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .ToList();

        var pageEditionIds = pageTargets
            .Where(target => target.Isbn13.HasValue)
            .Select(target => target.Isbn13!.Value)
            .Distinct()
            .ToArray();
        List<Book> books = pageEditionIds.Length == 0
            ? []
            : await dbContext.Books.AsNoTracking()
                .Where(book => pageEditionIds.Contains(book.Id))
                .ToListAsync(cancellationToken);
        var booksByIsbn = books.ToDictionary(book => book.Id.Value);

        var pageRareBookIds = pageTargets
            .Where(target => target.RareBookId is not null)
            .Select(target => target.RareBookId!)
            .Distinct()
            .ToArray();
        List<RareBook> rareBooks = pageRareBookIds.Length == 0
            ? []
            : await dbContext.RareBooks.AsNoTracking()
                .Include(book => book.Photos)
                .Where(book => pageRareBookIds.Contains(book.Id))
                .ToListAsync(cancellationToken);
        var rareBooksById = rareBooks.ToDictionary(book => book.Id.Value);

        var comments = await LoadCommentsAsync(pageTargets, cancellationToken);
        var commentLookup = comments
            .GroupBy(row => TargetKey(row.Isbn13, row.RareBookId))
            .ToDictionary(
                group => group.Key,
                group => (IReadOnlyList<NotFoundReportCommentResult>)group
                    .Select(row => new NotFoundReportCommentResult(
                        row.Text,
                        row.Location?.ToString(),
                        ToOffset(row.ReportedAt)))
                    .ToArray());

        var items = pageTargets.Select(target =>
        {
            Book? book = target.Isbn13 is { } isbn ? booksByIsbn.GetValueOrDefault(isbn.Value) : null;
            RareBook? rareBook = target.RareBookId is { } rareId
                ? rareBooksById.GetValueOrDefault(rareId.Value)
                : null;
            var coverUrl = book?.CoverUrl ?? rareBook?.Photos.OrderBy(photo => photo.Position)
                .Select(photo => photo.BlobUri.ToString())
                .FirstOrDefault();

            return new NotFoundReportTargetResult(
                target.Kind,
                target.Isbn13?.Value,
                target.RareBookId?.Value,
                book?.Title ?? rareBook?.Title ?? string.Empty,
                book?.Authors ?? rareBook?.AuthorMention,
                book?.Publisher ?? rareBook?.Publisher,
                book?.PublicationYear ?? rareBook?.PublicationYear,
                coverUrl,
                target.Genre ?? book?.Genre,
                book?.QuantityAvailable ?? (rareBook is null ? 0 : 1),
                target.ReportCount,
                target.MemberCount,
                ToOffset(target.FirstReportedAt),
                ToOffset(target.LastReportedAt),
                target.FirstReportedAt < overdueBefore,
                commentLookup.GetValueOrDefault(TargetKey(target.Isbn13, target.RareBookId)) ?? []);
        }).ToArray();

        return new NotFoundReportQueueResult(
            ToOffset(dateTimeProvider.UtcNow),
            totalCount,
            openReportCount,
            overdueCount,
            query.Page,
            query.PageSize,
            totalCount,
            items);
    }

    private async Task<List<QueueCommentProjection>> LoadCommentsAsync(
        IReadOnlyCollection<QueueTargetProjection> targets,
        CancellationToken cancellationToken)
    {
        var editionIds = targets.Where(target => target.Isbn13.HasValue)
            .Select(target => target.Isbn13!.Value)
            .Distinct()
            .ToArray();
        var rareBookIds = targets.Where(target => target.RareBookId is not null)
            .Select(target => target.RareBookId!)
            .Distinct()
            .ToArray();
        if (editionIds.Length == 0 && rareBookIds.Length == 0)
        {
            return [];
        }

        return await dbContext.BookNotFoundReports.AsNoTracking()
            .Where(report => report.Status == NotFoundReportStatus.Open && report.Comment != null &&
                ((report.Isbn13 != null && editionIds.Contains(report.Isbn13.Value)) ||
                 (report.RareBookId != null && rareBookIds.Contains(report.RareBookId))))
            .OrderBy(report => report.ReportedAt)
            .Select(report => new QueueCommentProjection
            {
                Isbn13 = report.Isbn13,
                RareBookId = report.RareBookId,
                Text = report.Comment!,
                Location = report.Location,
                ReportedAt = report.ReportedAt
            })
            .ToListAsync(cancellationToken);
    }

    private static string TargetKey(Isbn13? isbn13, RareBookId? rareBookId) =>
        isbn13 is { } edition
            ? $"edition:{edition.Value}"
            : $"rare:{rareBookId!.Value:D}";

    private static IOrderedQueryable<QueueTargetProjection> ApplyOrder(
        IQueryable<QueueTargetProjection> targets,
        string sort) => sort switch
    {
        "oldest" => targets.OrderBy(target => target.FirstReportedAt),
        "newest" => targets.OrderByDescending(target => target.LastReportedAt)
            .ThenByDescending(target => target.FirstReportedAt),
        "genre" => targets.OrderBy(target => target.Genre),
        _ => targets.OrderByDescending(target => target.ReportCount)
            .ThenBy(target => target.FirstReportedAt)
    };

    private static IOrderedEnumerable<QueueTargetProjection> OrderInMemory(
        IEnumerable<QueueTargetProjection> targets,
        string sort) => sort switch
    {
        "oldest" => targets.OrderBy(target => target.FirstReportedAt)
            .ThenBy(target => target.Kind),
        "newest" => targets.OrderByDescending(target => target.LastReportedAt)
            .ThenByDescending(target => target.FirstReportedAt).ThenBy(target => target.Kind),
        "genre" => targets.OrderBy(target => target.Genre)
            .ThenBy(target => target.Kind),
        _ => targets.OrderByDescending(target => target.ReportCount)
            .ThenBy(target => target.FirstReportedAt).ThenBy(target => target.Kind)
    };

    private static DateTimeOffset ToOffset(DateTime value) =>
        new(DateTime.SpecifyKind(value, DateTimeKind.Utc), TimeSpan.Zero);

    private sealed class QueueTargetProjection
    {
        public string Kind { get; init; } = string.Empty;
        public Isbn13? Isbn13 { get; init; }
        public RareBookId? RareBookId { get; init; }
        public string Title { get; init; } = string.Empty;
        public string? Authors { get; init; }
        public string? Publisher { get; init; }
        public int? PublicationYear { get; init; }
        public string? CoverUrl { get; init; }
        public string? Genre { get; init; }
        public int QuantityAvailable { get; init; }
        public int ReportCount { get; init; }
        public int MemberCount { get; init; }
        public DateTime FirstReportedAt { get; init; }
        public DateTime LastReportedAt { get; init; }
    }

    private sealed class QueueCommentProjection
    {
        public Isbn13? Isbn13 { get; init; }
        public RareBookId? RareBookId { get; init; }
        public string Text { get; init; } = string.Empty;
        public NotFoundReportLocation? Location { get; init; }
        public DateTime ReportedAt { get; init; }
    }
}
