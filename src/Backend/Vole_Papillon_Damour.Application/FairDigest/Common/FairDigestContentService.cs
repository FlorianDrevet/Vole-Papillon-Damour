using ErrorOr;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Vole_Papillon_Damour.Application.Books.Common;
using Vole_Papillon_Damour.Application.Common.Interfaces.Persistence;
using Vole_Papillon_Damour.Application.Common.Interfaces.Services;
using Vole_Papillon_Damour.Application.Recommendations.Queries.GetMyRecommendations;
using Vole_Papillon_Damour.Domain.BookAggregate;
using Vole_Papillon_Damour.Domain.BookAggregate.Entities;
using Vole_Papillon_Damour.Domain.BookAggregate.ValueObjects;
using Vole_Papillon_Damour.Domain.BookMovementAggregate.ValueObjects;
using Vole_Papillon_Damour.Domain.CheckoutPassageAggregate.ValueObjects;
using Vole_Papillon_Damour.Domain.Common.Errors;
using Vole_Papillon_Damour.Domain.FavoriteBookAggregate;
using Vole_Papillon_Damour.Domain.RecommendationAggregate;
using Vole_Papillon_Damour.Domain.UserAggregate;
using Vole_Papillon_Damour.Domain.WatchlistAggregate.ValueObjects;

namespace Vole_Papillon_Damour.Application.FairDigest.Common;

public sealed class FairDigestContentService(
    IProjectDbContext dbContext,
    IMediator mediator,
    IDateTimeProvider dateTimeProvider,
    IOptions<FairDigestOptions> options,
    FairDigestContentBuilder contentBuilder) : IFairDigestContentService
{
    public async Task<ErrorOr<FairDigestMemberContent>> BuildAsync(
        User member,
        FairDigestScheduleResult schedule,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(member);
        if (schedule.NextFair is null)
        {
            return Error.Validation("FairDigest.NoUpcomingFair", "A future book fair is required to build the digest.");
        }

        var nowUtc = dateTimeProvider.UtcNow;
        if (nowUtc.Kind != DateTimeKind.Utc)
        {
            return Error.Validation("FairDigest.InvalidClock", "The fair digest clock must be expressed in UTC.");
        }

        var excluded = await ReadMemberExclusionsAsync(member, cancellationToken);
        var arrivalIsbns = await FairDigestArrivalSelector.ReadArrivedIsbnsAsync(
            dbContext,
            schedule.PreviousFairEndsAtUtc?.UtcDateTime ?? DateTime.MinValue,
            cancellationToken);
        var parsedArrivalIsbns = arrivalIsbns
            .Select(value => Isbn13.TryCreate(value, out var isbn13) ? isbn13 : (Isbn13?)null)
            .Where(isbn13 => isbn13.HasValue)
            .Select(isbn13 => isbn13!.Value)
            .ToArray();
        if (parsedArrivalIsbns.Length == 0)
        {
            return contentBuilder.Build([], []);
        }

        var arrivedBooks = await dbContext.Books
            .AsNoTracking()
            .Where(book => parsedArrivalIsbns.Contains(book.Id))
            .ToListAsync(cancellationToken);
        var activeAnnouncements = await dbContext.BookAnnouncements
            .AsNoTracking()
            .Where(announcement =>
                parsedArrivalIsbns.Contains(announcement.Isbn13) &&
                announcement.Status == BookAnnouncementStatus.Announced)
            .ToListAsync(cancellationToken);
        var fairs = await dbContext.AssoEvents
            .AsNoTracking()
            .ToReferencedFairListAsync(activeAnnouncements, cancellationToken);
        var publicArrivals = PublicCatalogProjector.Project(
                arrivedBooks,
                activeAnnouncements,
                fairs,
                new HashSet<string>(StringComparer.Ordinal),
                nowUtc)
            .Where(book => book.QuantityAvailable > 0 || book.QuantityAnnounced > 0)
            .ToArray();

        IReadOnlyList<PersonalRecommendation> news = [];
        var preference = await dbContext.MemberRecommendationPreferences
            .AsNoTracking()
            .SingleOrDefaultAsync(candidate => candidate.UserId == member.Id, cancellationToken);
        if (preference?.Enabled != false &&
            !string.IsNullOrWhiteSpace(member.ExternalId) &&
            !string.IsNullOrWhiteSpace(member.Email) &&
            publicArrivals.Length > 0)
        {
            var recommendationResult = await mediator.Send(
                new GetMyRecommendationsQuery(
                    member.ExternalId,
                    member.Email,
                    member.Name?.FirstName,
                    member.Name?.LastName,
                    options.Value.NewsMax,
                    CandidateIsbns: publicArrivals.Select(book => book.Isbn13).ToArray()),
                cancellationToken);
            if (!recommendationResult.IsError)
            {
                news = recommendationResult.Value.Items
                    .Where(item => !excluded.Contains(item.Book))
                    .ToArray();
            }
        }

        var backItems = await BuildBackItemsAsync(
            publicArrivals,
            schedule.PreviousFair,
            schedule.PreviousFairEndsAtUtc,
            excluded,
            cancellationToken);
        return contentBuilder.Build(news, backItems);
    }

    private async Task<IReadOnlyList<FairDigestBackItem>> BuildBackItemsAsync(
        IReadOnlyCollection<PublicCatalogBookResult> publicArrivals,
        FairDigestEvent? previousFair,
        DateTimeOffset? previousFairEndsAtUtc,
        MemberExclusions excluded,
        CancellationToken cancellationToken)
    {
        if (publicArrivals.Count == 0 || previousFair is null || previousFairEndsAtUtc is null)
        {
            return [];
        }

        var arrivalWorkIds = publicArrivals
            .Where(book => !string.IsNullOrWhiteSpace(book.WorkId))
            .Select(book => book.WorkId!)
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        var arrivalIsbns = publicArrivals
            .Select(book => ParseIsbn(book.Isbn13))
            .ToArray();
        var workBooks = await dbContext.Books
            .AsNoTracking()
            .Where(book => arrivalIsbns.Contains(book.Id) ||
                           (book.WorkId != null && arrivalWorkIds.Contains(book.WorkId)))
            .Select(book => new BookInventorySnapshot(
                book.Id,
                book.WorkId,
                book.QuantityAvailable,
                book.SalesCount))
            .ToListAsync(cancellationToken);
        var workBookIsbns = workBooks.Select(book => book.Isbn13).Distinct().ToArray();
        var announcements = await dbContext.BookAnnouncements
            .AsNoTracking()
            .Where(announcement =>
                workBookIsbns.Contains(announcement.Isbn13) &&
                announcement.Status == BookAnnouncementStatus.Announced)
            .ToListAsync(cancellationToken);
        var activeFairIds = (await dbContext.AssoEvents
                .AsNoTracking()
                .WhereActiveBookFair()
                .Select(assoEvent => assoEvent.Id)
                .ToListAsync(cancellationToken))
            .Select(fairId => fairId.Value)
            .ToHashSet();
        var announcedByIsbn = announcements
            .Where(announcement =>
                announcement.AssoEventsId is null ||
                activeFairIds.Contains(announcement.AssoEventsId.Value))
            .GroupBy(announcement => announcement.Isbn13)
            .ToDictionary(group => group.Key, group => group.Sum(announcement => announcement.Quantity));

        var previousFairId = Vole_Papillon_Damour.Domain.EventsAggregate.ValueObjects.AssoEventsId
            .Create(previousFair.Id);
        var previousFairSales = await dbContext.BookMovements
            .AsNoTracking()
            .Where(movement =>
                movement.AssoEventsId == previousFairId &&
                movement.Type == BookMovementType.Sale &&
                movement.Quantity < 0)
            .Select(movement => new { movement.Isbn13, movement.Quantity })
            .ToListAsync(cancellationToken);
        var saleIsbns = previousFairSales.Select(sale => sale.Isbn13).Distinct().ToArray();
        var salesWorkIds = saleIsbns.Length == 0
            ? []
            : await dbContext.Books
                .AsNoTracking()
                .Where(book => saleIsbns.Contains(book.Id))
                .Select(book => new { book.Id, book.WorkId })
                .ToListAsync(cancellationToken);
        var salesGroupByIsbn = salesWorkIds.ToDictionary(
            book => book.Id,
            book => GroupKey(book.WorkId, book.Id.Value));
        var salesByWork = previousFairSales
            .GroupBy(sale => salesGroupByIsbn.GetValueOrDefault(sale.Isbn13, GroupKey(null, sale.Isbn13.Value)))
            .ToDictionary(group => group.Key, group => group.Sum(sale => -sale.Quantity), StringComparer.Ordinal);
        var topSellerKeys = salesByWork
            .Where(sale => sale.Value > 0)
            .OrderByDescending(sale => sale.Value)
            .ThenBy(sale => sale.Key, StringComparer.Ordinal)
            .Take(options.Value.TopSellersCount)
            .Select(sale => sale.Key)
            .ToHashSet(StringComparer.Ordinal);

        var inventoryByIsbn = workBooks.ToDictionary(
            book => book.Isbn13,
            book => book.QuantityAvailable + announcedByIsbn.GetValueOrDefault(book.Isbn13));
        var workBookIds = workBookIsbns;
        var laterMovements = await dbContext.BookMovements
            .AsNoTracking()
            .Where(movement =>
                workBookIds.Contains(movement.Isbn13) &&
                movement.OccurredAt > previousFairEndsAtUtc.Value.UtcDateTime)
            .Select(movement => new { movement.Isbn13, movement.Type, movement.Quantity })
            .ToListAsync(cancellationToken);
        var stockAfterPreviousFair = laterMovements
            .GroupBy(movement => movement.Isbn13)
            .ToDictionary(
                group => group.Key,
                group => group.Sum(movement => InventoryDelta(movement.Type, movement.Quantity)));
        var stockAtPreviousFairByWork = workBooks
            .GroupBy(book => GroupKey(book.WorkId, book.Isbn13.Value))
            .ToDictionary(
                group => group.Key,
                group => group.Sum(book => Math.Max(
                    0,
                    inventoryByIsbn.GetValueOrDefault(book.Isbn13) -
                    stockAfterPreviousFair.GetValueOrDefault(book.Isbn13))));
        var totalSalesByWork = workBooks
            .GroupBy(book => GroupKey(book.WorkId, book.Isbn13.Value))
            .ToDictionary(group => group.Key, group => group.Sum(book => book.SalesCount), StringComparer.Ordinal);

        var backCandidates = new List<FairDigestBackItem>();
        foreach (var group in publicArrivals.GroupBy(book => GroupKey(book.WorkId, book.Isbn13)))
        {
            var groupKey = group.Key;
            var soldOut = stockAtPreviousFairByWork.GetValueOrDefault(groupKey) == 0 &&
                          totalSalesByWork.GetValueOrDefault(groupKey) >= options.Value.SoldOutMinSales;
            var topSeller = topSellerKeys.Contains(groupKey);
            if (!soldOut && !topSeller)
            {
                continue;
            }

            var representative = group
                .OrderByDescending(book => book.QuantityAvailable + book.QuantityAnnounced)
                .ThenBy(book => book.Isbn13, StringComparer.Ordinal)
                .First();
            if (!excluded.Contains(representative))
            {
                backCandidates.Add(new FairDigestBackItem(
                    representative,
                    salesByWork.GetValueOrDefault(groupKey),
                    IsVeryDemanded: true));
            }
        }

        return backCandidates;
    }

    private async Task<MemberExclusions> ReadMemberExclusionsAsync(
        User member,
        CancellationToken cancellationToken)
    {
        var purchasedIsbns = await (
                from passage in dbContext.CheckoutPassages.AsNoTracking()
                join line in dbContext.CheckoutPassageLines.AsNoTracking()
                    on passage.Id equals line.CheckoutPassageId
                where passage.UserId == member.Id &&
                      passage.Status == CheckoutPassageStatus.Associated &&
                      line.VoidedAt == null &&
                      line.Isbn13 != null
                select line.Isbn13!.Value)
            .ToListAsync(cancellationToken);
        var favoriteIsbns = await dbContext.MemberFavoriteBooks
            .AsNoTracking()
            .Where(favorite => favorite.UserId == member.Id)
            .Select(favorite => favorite.Isbn13)
            .ToListAsync(cancellationToken);
        var selectionIsbns = await dbContext.MemberSelectionItems
            .AsNoTracking()
            .Where(item => item.UserId == member.Id && item.Isbn13 != null)
            .Select(item => item.Isbn13!.Value)
            .ToListAsync(cancellationToken);
        var watchlistEntries = await dbContext.WatchlistItems
            .AsNoTracking()
            .Where(item => item.UserId == member.Id)
            .Select(item => new { item.Scope, item.Isbn13, item.WorkId })
            .ToListAsync(cancellationToken);

        var excludedIsbns = purchasedIsbns
            .Concat(favoriteIsbns)
            .Concat(selectionIsbns)
            .Concat(watchlistEntries
                .Where(item => item.Scope == WatchlistItemScope.Edition && item.Isbn13 != null)
                .Select(item => item.Isbn13!.Value))
            .Select(isbn => isbn.Value)
            .ToHashSet(StringComparer.Ordinal);
        var excludedWorkIds = watchlistEntries
            .Where(item => item.Scope == WatchlistItemScope.Work && !string.IsNullOrWhiteSpace(item.WorkId))
            .Select(item => item.WorkId!)
            .ToHashSet(StringComparer.Ordinal);
        if (excludedIsbns.Count > 0)
        {
            var exclusionIsbnValues = excludedIsbns
                .Select(ParseIsbn)
                .ToArray();
            var additionalWorkIds = await dbContext.Books
                .AsNoTracking()
                .Where(book => exclusionIsbnValues.Contains(book.Id) && book.WorkId != null)
                .Select(book => book.WorkId)
                .ToListAsync(cancellationToken);
            excludedWorkIds.UnionWith(additionalWorkIds.Where(id => !string.IsNullOrWhiteSpace(id))!);
        }

        return new MemberExclusions(excludedIsbns, excludedWorkIds);
    }

    private static string GroupKey(string? workId, string isbn) =>
        string.IsNullOrWhiteSpace(workId) ? $"isbn:{isbn}" : $"work:{workId}";

    private static Isbn13 ParseIsbn(string value)
    {
        if (!Isbn13.TryCreate(value, out var isbn13))
        {
            throw new InvalidOperationException("Fair digest ISBNs must be valid ISBN-13 values.");
        }

        return isbn13;
    }

    private static int InventoryDelta(BookMovementType type, int quantity) => type switch
    {
        BookMovementType.DirectEntry or BookMovementType.AnnouncementEntry => quantity,
        BookMovementType.Sale or BookMovementType.Withdrawal or BookMovementType.Correction => quantity,
        _ => 0,
    };

    private sealed record BookInventorySnapshot(
        Isbn13 Isbn13,
        string? WorkId,
        int QuantityAvailable,
        int SalesCount);

    private sealed record MemberExclusions(HashSet<string> Isbns, HashSet<string> WorkIds)
    {
        public bool Contains(PublicCatalogBookResult book) =>
            Isbns.Contains(book.Isbn13) ||
            (book.WorkId is not null && WorkIds.Contains(book.WorkId));
    }
}
