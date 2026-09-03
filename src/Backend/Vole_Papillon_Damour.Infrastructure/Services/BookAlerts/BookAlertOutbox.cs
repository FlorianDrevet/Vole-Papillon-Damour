using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using Vole_Papillon_Damour.Application.Common.Interfaces.Persistence;
using Vole_Papillon_Damour.Domain.AssociationSettingsAggregate;
using Vole_Papillon_Damour.Domain.BookAggregate.ValueObjects;
using Vole_Papillon_Damour.Domain.BookMovementAggregate.ValueObjects;
using Vole_Papillon_Damour.Domain.ScanSessionAggregate.ValueObjects;
using Vole_Papillon_Damour.Domain.UserAggregate.ValueObjects;
using Vole_Papillon_Damour.Domain.WatchlistAggregate;
using Vole_Papillon_Damour.Domain.WatchlistAggregate.ValueObjects;
using Vole_Papillon_Damour.Infrastructure.Persistence;
using Vole_Papillon_Damour.Infrastructure.Persistence.Outbox;

namespace Vole_Papillon_Damour.Infrastructure.Services.BookAlerts;

public sealed class BookAlertOutbox(ProjectDbContext dbContext) : IBookAlertOutbox
{
    private static readonly JsonSerializerOptions PayloadSerializerOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    public async Task QueueForSessionAsync(
        ScanSessionId scanSessionId,
        DateTime closedAt,
        CancellationToken cancellationToken)
    {
        if (scanSessionId is null)
        {
            throw new ArgumentNullException(nameof(scanSessionId));
        }

        if (closedAt.Kind != DateTimeKind.Utc)
        {
            throw new ArgumentException("The close time must be expressed in UTC.", nameof(closedAt));
        }

        var session = await dbContext.ScanSessions
            .AsNoTracking()
            .SingleOrDefaultAsync(
                candidate => candidate.Id == scanSessionId,
                cancellationToken);
        if (session is null ||
            (session.Mode == ScanMode.NextFair && session.TargetAssoEventsId is null))
        {
            return;
        }

        var settings = await dbContext.AssociationSettings
            .AsNoTracking()
            .SingleOrDefaultAsync(
                candidate => candidate.Id == AssociationSettings.SingletonId,
                cancellationToken);
        var alertDelayMinutes = settings?.AlertDelayMinutes ?? 120;
        var alertCooldownDays = settings?.AlertCooldownDays ?? 30;

        var movementType = session.Mode == ScanMode.AvailableNow
            ? BookMovementType.DirectEntry
            : BookMovementType.AnnouncementEntry;
        var movements = await dbContext.BookMovements
            .AsNoTracking()
            .Where(movement =>
                movement.ScanSessionId == scanSessionId &&
                movement.Type == movementType &&
                movement.Quantity > 0 &&
                (session.Mode != ScanMode.NextFair || movement.AssoEventsId != null))
            .OrderBy(movement => movement.OccurredAt)
            .ThenBy(movement => movement.Id)
            .ToListAsync(cancellationToken);
        if (movements.Count == 0)
        {
            return;
        }

        var isbn13s = movements
            .Select(movement => movement.Isbn13)
            .Distinct()
            .ToArray();
        var books = await dbContext.Books
            .AsNoTracking()
            .Where(book => isbn13s.Contains(book.Id))
            .ToDictionaryAsync(book => book.Id, cancellationToken);

        var candidates = movements
            .GroupBy(movement => movement.Isbn13)
            .Select(group =>
            {
                var book = books.GetValueOrDefault(group.Key);
                var firstMovement = group.First();
                return new AlertCandidate(
                    group.Key,
                    book?.WorkId,
                    book?.Title,
                    book?.Authors,
                    group.Sum(movement => movement.Quantity),
                    session.Mode,
                    firstMovement.AssoEventsId);
            })
            .ToArray();

        var activeWatchlists = await dbContext.Watchlists
            .AsNoTracking()
            .Where(watchlist => watchlist.AlertStatus == WatchlistAlertStatus.Active)
            .ToListAsync(cancellationToken);
        if (activeWatchlists.Count == 0)
        {
            return;
        }

        var activeMemberIds = activeWatchlists
            .Select(watchlist => watchlist.Id)
            .ToArray();
        var watchlistItems = await dbContext.WatchlistItems
            .AsNoTracking()
            .Where(item => activeMemberIds.Contains(item.UserId))
            .ToListAsync(cancellationToken);
        if (watchlistItems.Count == 0)
        {
            return;
        }

        var cooldownStart = closedAt.AddDays(-alertCooldownDays);
        var recentHistory = await dbContext.UserAlertHistories
            .AsNoTracking()
            .Where(history =>
                activeMemberIds.Contains(history.UserId) &&
                history.SentAt >= cooldownStart &&
                history.SentAt <= closedAt)
            .ToListAsync(cancellationToken);

        var matchedItems = new Dictionary<UserId, List<AlertCandidate>>();
        foreach (var item in watchlistItems)
        {
            foreach (var candidate in candidates)
            {
                if (!Matches(item, candidate) ||
                    recentHistory.Any(history =>
                        history.UserId == item.UserId && history.Isbn13 == candidate.Isbn13))
                {
                    continue;
                }

                if (!matchedItems.TryGetValue(item.UserId, out var memberItems))
                {
                    memberItems = [];
                    matchedItems[item.UserId] = memberItems;
                }

                if (memberItems.All(existing => existing.Isbn13 != candidate.Isbn13))
                {
                    memberItems.Add(candidate);
                }
            }
        }

        var dueAt = closedAt.AddMinutes(alertDelayMinutes);
        foreach (var member in matchedItems.OrderBy(pair => pair.Key.Value))
        {
            var items = member.Value
                .OrderBy(candidate => candidate.Isbn13.Value, StringComparer.Ordinal)
                .Select(candidate => new AlertItemPayload(
                    candidate.Isbn13.Value,
                    candidate.Title,
                    candidate.Authors,
                    candidate.Quantity,
                    candidate.Mode,
                    candidate.AssoEventsId?.Value))
                .ToArray();
            dbContext.OutboxMessages.Add(new OutboxMessage
            {
                Id = Guid.NewGuid(),
                Kind = OutboxMessageKind.AlertEmail,
                PayloadJson = JsonSerializer.Serialize(
                    new AlertEmailPayload(items),
                    PayloadSerializerOptions),
                DueAt = dueAt,
                Status = OutboxMessageStatus.Pending,
                Attempts = 0,
                ScanSessionId = scanSessionId.Value,
                MemberId = member.Key.Value,
                CreatedAt = closedAt
            });
        }
    }

    public async Task<int> CancelPendingForSessionAsync(
        ScanSessionId scanSessionId,
        CancellationToken cancellationToken)
    {
        ValidateScanSessionId(scanSessionId);

        var messages = await GetPendingMessagesForSessionAsync(scanSessionId, cancellationToken);
        foreach (var message in messages)
        {
            message.Status = OutboxMessageStatus.Cancelled;
            message.ClaimedUntil = null;
        }

        return messages.Count;
    }

    public async Task<int> ForcePendingForSessionAsync(
        ScanSessionId scanSessionId,
        DateTime dueAt,
        CancellationToken cancellationToken)
    {
        ValidateScanSessionId(scanSessionId);
        if (dueAt.Kind != DateTimeKind.Utc)
        {
            throw new ArgumentException("The due time must be expressed in UTC.", nameof(dueAt));
        }

        var messages = await GetPendingMessagesForSessionAsync(scanSessionId, cancellationToken);
        foreach (var message in messages)
        {
            message.DueAt = dueAt;
            message.ClaimedUntil = null;
            message.LastError = null;
        }

        return messages.Count;
    }

    private async Task<List<OutboxMessage>> GetPendingMessagesForSessionAsync(
        ScanSessionId scanSessionId,
        CancellationToken cancellationToken)
    {
        return await dbContext.OutboxMessages
            .Where(message =>
                message.Kind == OutboxMessageKind.AlertEmail &&
                message.ScanSessionId == scanSessionId.Value &&
                message.Status == OutboxMessageStatus.Pending)
            .OrderBy(message => message.DueAt)
            .ThenBy(message => message.Id)
            .ToListAsync(cancellationToken);
    }

    private static void ValidateScanSessionId(ScanSessionId scanSessionId)
    {
        if (scanSessionId is null)
        {
            throw new ArgumentNullException(nameof(scanSessionId));
        }
    }

    private static bool Matches(WatchlistItem item, AlertCandidate candidate)
    {
        return item.Scope switch
        {
            WatchlistItemScope.Edition => item.Isbn13 == candidate.Isbn13,
            WatchlistItemScope.Work => item.WorkId is not null &&
                                        item.WorkId == candidate.WorkId,
            _ => false
        };
    }

    private sealed record AlertCandidate(
        Isbn13 Isbn13,
        string? WorkId,
        string? Title,
        string? Authors,
        int Quantity,
        ScanMode Mode,
        Vole_Papillon_Damour.Domain.EventsAggregate.ValueObjects.AssoEventsId? AssoEventsId);

    private sealed record AlertEmailPayload(IReadOnlyList<AlertItemPayload> Items);

    private sealed record AlertItemPayload(
        string Isbn13,
        string? Title,
        string? Authors,
        int Quantity,
        ScanMode Mode,
        Guid? AssoEventsId);
}
