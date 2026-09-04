using System.Data;
using System.Data.Common;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using Vole_Papillon_Damour.Application.Common.Models;
using Vole_Papillon_Damour.Application.Common.Interfaces.Persistence;
using Vole_Papillon_Damour.Domain.AssociationSettingsAggregate;
using Vole_Papillon_Damour.Domain.AssoEventsAggregate;
using Vole_Papillon_Damour.Domain.BookAggregate.ValueObjects;
using Vole_Papillon_Damour.Domain.BookMovementAggregate.ValueObjects;
using Vole_Papillon_Damour.Domain.EventsAggregate.ValueObjects;
using Vole_Papillon_Damour.Domain.ScanSessionAggregate.ValueObjects;
using Vole_Papillon_Damour.Domain.UserAggregate.ValueObjects;
using Vole_Papillon_Damour.Domain.WatchlistAggregate;
using Vole_Papillon_Damour.Domain.WatchlistAggregate.ValueObjects;
using Vole_Papillon_Damour.Infrastructure.Persistence;
using Vole_Papillon_Damour.Infrastructure.Persistence.Configurations;
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

        var bookFairs = await dbContext.AssoEvents
            .AsNoTracking()
            .ToListAsync(cancellationToken);
        var activeBookFairOpenings = bookFairs
            .Where(assoEvent =>
                !assoEvent.IsCancelled &&
                assoEvent.EventsType.Value == EventsType.EventsTypeEnum.Books)
            .ToDictionary(
                assoEvent => assoEvent.Id.Value,
                GetOpeningInstant);
        var nextBookFairOpening = activeBookFairOpenings.Values
            .Where(opening => opening > new DateTimeOffset(closedAt))
            .OrderBy(opening => opening)
            .Select(opening => (DateTimeOffset?)opening)
            .FirstOrDefault();

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
                    book?.Publisher,
                    book?.PublicationYear,
                    book?.PhysicalFormat,
                    group.Sum(movement => movement.Quantity),
                    session.Mode,
                    firstMovement.AssoEventsId,
                    firstMovement.AssoEventsId is { } fairId &&
                    activeBookFairOpenings.TryGetValue(fairId.Value, out var fairOpening)
                        ? fairOpening
                        : session.Mode == ScanMode.AvailableNow
                            ? nextBookFairOpening
                            : null);
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
                    candidate.WorkId,
                    candidate.Title,
                    candidate.Authors,
                    candidate.Publisher,
                    candidate.PublicationYear,
                    candidate.PhysicalFormat,
                    candidate.Quantity,
                    candidate.Mode,
                    candidate.AssoEventsId?.Value,
                    candidate.FairOpeningAt))
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

    public async Task<IReadOnlyList<BookAlertOutboxWorkItem>> ClaimDueAsync(
        DateTime now,
        TimeSpan lease,
        int batchSize,
        CancellationToken cancellationToken)
    {
        ValidateUtc(now, nameof(now));
        if (lease <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(lease));
        }

        if (batchSize <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(batchSize));
        }

        var connection = dbContext.Database.GetDbConnection();
        if (connection.GetType().Name.Contains("Sqlite", StringComparison.OrdinalIgnoreCase))
        {
            return await ClaimDueWithEntityFrameworkAsync(
                now,
                lease,
                batchSize,
                cancellationToken);
        }

        var shouldCloseConnection = connection.State != ConnectionState.Open;
        if (shouldCloseConnection)
        {
            await connection.OpenAsync(cancellationToken);
        }

        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText = """
                ;WITH candidates AS
                (
                    SELECT TOP (@batchSize) [Id]
                    FROM [OutboxMessages] WITH (UPDLOCK, READPAST, ROWLOCK)
                    WHERE [Kind] = @alertEmailKind
                      AND [Status] = @pendingStatus
                      AND [DueAt] <= @now
                      AND ([ClaimedUntil] IS NULL OR [ClaimedUntil] < @now)
                    ORDER BY [DueAt], [Id]
                )
                UPDATE message
                   SET [ClaimedUntil] = @leaseUntil,
                       [Attempts] = [Attempts] + 1,
                       [LastError] = NULL
                OUTPUT inserted.[Id], inserted.[MemberId], inserted.[PayloadJson], inserted.[Attempts], inserted.[ClaimedUntil]
                  FROM [OutboxMessages] AS message
                  INNER JOIN candidates ON candidates.[Id] = message.[Id];
                """;
            AddParameter(command, "@batchSize", batchSize, DbType.Int32);
            AddParameter(command, "@alertEmailKind", (byte)OutboxMessageKind.AlertEmail, DbType.Byte);
            AddParameter(command, "@pendingStatus", (byte)OutboxMessageStatus.Pending, DbType.Byte);
            AddParameter(command, "@now", now, DbType.DateTime2);
            AddParameter(command, "@leaseUntil", now.Add(lease), DbType.DateTime2);

            var workItems = new List<BookAlertOutboxWorkItem>();
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                if (reader.IsDBNull(1))
                {
                    throw new InvalidOperationException("An alert outbox message has no member.");
                }

                if (reader.IsDBNull(4))
                {
                    throw new InvalidOperationException("An alert outbox claim has no lease expiry.");
                }

                workItems.Add(ToWorkItem(
                    reader.GetGuid(0),
                    reader.GetGuid(1),
                    reader.GetString(2),
                    reader.GetInt32(3),
                    reader.GetDateTime(4)));
            }

            return workItems;
        }
        finally
        {
            if (shouldCloseConnection)
            {
                await connection.CloseAsync();
            }
        }
    }

    public async Task<DateTime?> GetOldestDueAtAsync(
        DateTime now,
        CancellationToken cancellationToken)
    {
        ValidateUtc(now, nameof(now));
        return await dbContext.OutboxMessages
            .AsNoTracking()
            .Where(message =>
                message.Kind == OutboxMessageKind.AlertEmail &&
                message.Status == OutboxMessageStatus.Pending &&
                message.DueAt <= now)
            .Select(message => (DateTime?)message.DueAt)
            .MinAsync(cancellationToken);
    }

    public async Task<BookAlertDelivery?> GetPendingDeliveryAsync(
        Guid messageId,
        DateTime claimedUntil,
        DateTime now,
        CancellationToken cancellationToken)
    {
        ValidateMessageId(messageId);
        ValidateUtc(claimedUntil, nameof(claimedUntil));
        ValidateUtc(now, nameof(now));

        var message = await dbContext.OutboxMessages
            .SingleOrDefaultAsync(
                candidate =>
                    candidate.Id == messageId &&
                    candidate.Kind == OutboxMessageKind.AlertEmail &&
                    candidate.Status == OutboxMessageStatus.Pending &&
                    candidate.ClaimedUntil != null &&
                    candidate.ClaimedUntil == claimedUntil &&
                    candidate.ClaimedUntil >= now,
                cancellationToken);
        if (message is null || message.MemberId is null)
        {
            return null;
        }

        var memberId = UserId.Create(message.MemberId.Value);
        var user = await dbContext.Users
            .AsNoTracking()
            .SingleOrDefaultAsync(candidate => candidate.Id == memberId, cancellationToken);
        var watchlist = await dbContext.Watchlists
            .AsNoTracking()
            .SingleOrDefaultAsync(
                candidate => candidate.Id == memberId &&
                             candidate.AlertStatus == WatchlistAlertStatus.Active,
                cancellationToken);
        if (user is null || watchlist is null || string.IsNullOrWhiteSpace(user.Email))
        {
            return null;
        }

        var payload = DeserializePayload(message.PayloadJson);
        var watchlistItems = await dbContext.WatchlistItems
            .AsNoTracking()
            .Where(item => item.UserId == memberId)
            .ToListAsync(cancellationToken);
        if (watchlistItems.Count == 0)
        {
            return null;
        }

        var payloadItems = payload.Items ?? [];
        var payloadFairIds = payloadItems
            .Where(item => item.AssoEventsId is not null)
            .Select(item => item.AssoEventsId!.Value)
            .ToHashSet();
        HashSet<Guid> activeBookFairIds = [];
        if (payloadFairIds.Count > 0)
        {
            var fairs = await dbContext.AssoEvents
                .AsNoTracking()
                .ToListAsync(cancellationToken);
            activeBookFairIds = fairs
                .Where(fair =>
                    !fair.IsCancelled &&
                    fair.EventsType.Value == EventsType.EventsTypeEnum.Books &&
                    payloadFairIds.Contains(fair.Id.Value))
                .Select(fair => fair.Id.Value)
                .ToHashSet();
        }

        var itemIsbns = payloadItems
            .Select(item => BookPersistenceConversions.ParseIsbn13(item.Isbn13))
            .ToArray();
        var settings = await dbContext.AssociationSettings
            .AsNoTracking()
            .SingleOrDefaultAsync(
                candidate => candidate.Id == AssociationSettings.SingletonId,
                cancellationToken);
        var cooldownDays = settings?.AlertCooldownDays ?? 30;
        var recentHistory = await dbContext.UserAlertHistories
            .AsNoTracking()
            .Where(history =>
                history.UserId == memberId &&
                itemIsbns.Contains(history.Isbn13) &&
                history.SentAt >= now.AddDays(-cooldownDays) &&
                history.SentAt <= now)
            .ToListAsync(cancellationToken);

        var eligibleItems = payloadItems
            .Where(item =>
            {
                var isbn13 = BookPersistenceConversions.ParseIsbn13(item.Isbn13);
                return watchlistItems.Any(watchlistItem =>
                           Matches(watchlistItem, item, isbn13)) &&
                       (item.AssoEventsId is null ||
                        activeBookFairIds.Contains(item.AssoEventsId.Value)) &&
                       recentHistory.All(history => history.Isbn13 != isbn13);
            })
            .Select(ToApplicationItem)
            .ToArray();
        if (eligibleItems.Length == 0)
        {
            return null;
        }

        var recipientName = user.Name is null
            ? null
            : $"{user.Name.FirstName} {user.Name.LastName}".Trim();
        return new BookAlertDelivery(
            message.Id,
            memberId.Value,
            user.Email,
            string.IsNullOrWhiteSpace(recipientName) ? null : recipientName,
            eligibleItems);
    }

    public async Task<int> CancelAsync(
        Guid messageId,
        DateTime claimedUntil,
        CancellationToken cancellationToken)
    {
        ValidateMessageId(messageId);
        ValidateUtc(claimedUntil, nameof(claimedUntil));
        var message = await dbContext.OutboxMessages
            .SingleOrDefaultAsync(
                candidate =>
                    candidate.Id == messageId &&
                    candidate.Kind == OutboxMessageKind.AlertEmail &&
                    candidate.Status == OutboxMessageStatus.Pending &&
                    candidate.ClaimedUntil != null &&
                    candidate.ClaimedUntil == claimedUntil,
                cancellationToken);
        if (message is null)
        {
            return 0;
        }

        message.Status = OutboxMessageStatus.Cancelled;
        message.ClaimedUntil = null;
        await dbContext.SaveChangesAsync(cancellationToken);
        return 1;
    }

    public async Task<bool> MarkSentAsync(
        Guid messageId,
        DateTime claimedUntil,
        DateTime sentAt,
        IReadOnlyCollection<Isbn13> itemIsbn13s,
        CancellationToken cancellationToken)
    {
        ValidateMessageId(messageId);
        ValidateUtc(claimedUntil, nameof(claimedUntil));
        ValidateUtc(sentAt, nameof(sentAt));
        ArgumentNullException.ThrowIfNull(itemIsbn13s);

        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        var message = await dbContext.OutboxMessages
            .SingleOrDefaultAsync(
                candidate =>
                    candidate.Id == messageId &&
                    candidate.Kind == OutboxMessageKind.AlertEmail &&
                    candidate.Status == OutboxMessageStatus.Pending &&
                    candidate.ClaimedUntil != null &&
                    candidate.ClaimedUntil == claimedUntil &&
                    candidate.ClaimedUntil >= sentAt,
                cancellationToken);
        if (message is null || message.MemberId is null)
        {
            await transaction.CommitAsync(cancellationToken);
            return false;
        }

        var memberId = UserId.Create(message.MemberId.Value);
        var settings = await dbContext.AssociationSettings
            .AsNoTracking()
            .SingleOrDefaultAsync(
                candidate => candidate.Id == AssociationSettings.SingletonId,
                cancellationToken);
        var cooldownDays = settings?.AlertCooldownDays ?? 30;
        var cooldownStart = sentAt.AddDays(-cooldownDays);
        var existingHistory = await dbContext.UserAlertHistories
            .Where(history =>
                history.UserId == memberId &&
                history.SentAt >= cooldownStart &&
                history.SentAt <= sentAt)
            .ToListAsync(cancellationToken);
        foreach (var isbn13 in itemIsbn13s.Distinct())
        {
            if (existingHistory.Any(history => history.Isbn13 == isbn13))
            {
                continue;
            }

            var history = UserAlertHistory.Create(
                Guid.NewGuid(),
                memberId,
                isbn13,
                sentAt,
                message.Id);
            dbContext.UserAlertHistories.Add(history);
            existingHistory.Add(history);
        }

        message.Status = OutboxMessageStatus.Sent;
        message.SentAt = sentAt;
        message.ClaimedUntil = null;
        message.LastError = null;
        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return true;
    }

    public async Task RecordFailureAsync(
        Guid messageId,
        DateTime claimedUntil,
        string failureCode,
        DateTime failedAt,
        CancellationToken cancellationToken)
    {
        ValidateMessageId(messageId);
        ValidateUtc(claimedUntil, nameof(claimedUntil));
        ValidateUtc(failedAt, nameof(failedAt));
        ArgumentException.ThrowIfNullOrWhiteSpace(failureCode);

        var message = await dbContext.OutboxMessages
            .SingleOrDefaultAsync(
                candidate =>
                    candidate.Id == messageId &&
                    candidate.Kind == OutboxMessageKind.AlertEmail &&
                    candidate.Status == OutboxMessageStatus.Pending &&
                    candidate.ClaimedUntil != null &&
                    candidate.ClaimedUntil == claimedUntil &&
                    candidate.ClaimedUntil >= failedAt,
                cancellationToken);
        if (message is null)
        {
            return;
        }

        const int maximumAttempts = 5;
        message.ClaimedUntil = null;
        message.LastError = failureCode.Length <= 128 ? failureCode : failureCode[..128];
        if (message.Attempts >= maximumAttempts)
        {
            message.Status = OutboxMessageStatus.Failed;
        }
        else
        {
            var delayMinutes = Math.Min(60, 5 * Math.Pow(2, Math.Max(0, message.Attempts - 1)));
            message.DueAt = failedAt.AddMinutes(delayMinutes);
        }

        await dbContext.SaveChangesAsync(cancellationToken);
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

    private async Task<IReadOnlyList<BookAlertOutboxWorkItem>> ClaimDueWithEntityFrameworkAsync(
        DateTime now,
        TimeSpan lease,
        int batchSize,
        CancellationToken cancellationToken)
    {
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        var messages = await dbContext.OutboxMessages
            .Where(message =>
                message.Kind == OutboxMessageKind.AlertEmail &&
                message.Status == OutboxMessageStatus.Pending &&
                message.DueAt <= now &&
                (message.ClaimedUntil == null || message.ClaimedUntil < now))
            .OrderBy(message => message.DueAt)
            .ThenBy(message => message.Id)
            .Take(batchSize)
            .ToListAsync(cancellationToken);

        foreach (var message in messages)
        {
            if (message.MemberId is null)
            {
                throw new InvalidOperationException("An alert outbox message has no member.");
            }

            message.ClaimedUntil = now.Add(lease);
            message.Attempts++;
            message.LastError = null;
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return messages
            .Select(message => ToWorkItem(
                message.Id,
                message.MemberId!.Value,
                message.PayloadJson,
                message.Attempts,
                message.ClaimedUntil!.Value))
            .ToArray();
    }

    private static BookAlertOutboxWorkItem ToWorkItem(
        Guid messageId,
        Guid memberId,
        string payloadJson,
        int attempts,
        DateTime claimedUntil)
    {
        claimedUntil = DateTime.SpecifyKind(claimedUntil, DateTimeKind.Utc);
        var payload = DeserializePayload(payloadJson);
        return new BookAlertOutboxWorkItem(
            messageId,
            memberId,
            (payload.Items ?? []).Select(ToApplicationItem).ToArray(),
            attempts,
            claimedUntil);
    }

    private static AlertEmailPayload DeserializePayload(string payloadJson)
    {
        return JsonSerializer.Deserialize<AlertEmailPayload>(payloadJson, PayloadSerializerOptions)
            ?? throw new InvalidOperationException("The alert outbox payload is invalid.");
    }

    private static BookAlertOutboxItem ToApplicationItem(AlertItemPayload item)
    {
        return new BookAlertOutboxItem(
            BookPersistenceConversions.ParseIsbn13(item.Isbn13),
            item.WorkId,
            item.Title,
            item.Authors,
            item.Quantity,
            item.Mode,
            item.AssoEventsId,
            item.Publisher,
            item.PublicationYear,
            item.PhysicalFormat,
            item.FairOpeningAt);
    }

    private static bool Matches(
        WatchlistItem item,
        AlertItemPayload candidate,
        Isbn13 isbn13)
    {
        return item.Scope switch
        {
            WatchlistItemScope.Edition => item.Isbn13 == isbn13,
            WatchlistItemScope.Work => item.WorkId is not null &&
                                        item.WorkId == candidate.WorkId,
            _ => false
        };
    }

    private static void AddParameter(DbCommand command, string name, object value, DbType type)
    {
        var parameter = command.CreateParameter();
        parameter.ParameterName = name;
        parameter.DbType = type;
        parameter.Value = value;
        command.Parameters.Add(parameter);
    }

    private static void ValidateUtc(DateTime value, string parameterName)
    {
        if (value.Kind != DateTimeKind.Utc)
        {
            throw new ArgumentException("The time must be expressed in UTC.", parameterName);
        }
    }

    private static void ValidateMessageId(Guid messageId)
    {
        if (messageId == Guid.Empty)
        {
            throw new ArgumentException("A message identifier is required.", nameof(messageId));
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
        string? Publisher,
        int? PublicationYear,
        string? PhysicalFormat,
        int Quantity,
        ScanMode Mode,
        Vole_Papillon_Damour.Domain.EventsAggregate.ValueObjects.AssoEventsId? AssoEventsId,
        DateTimeOffset? FairOpeningAt);

    private sealed record AlertEmailPayload(IReadOnlyList<AlertItemPayload>? Items);

    private sealed record AlertItemPayload(
        string Isbn13,
        string? WorkId,
        string? Title,
        string? Authors,
        string? Publisher,
        int? PublicationYear,
        string? PhysicalFormat,
        int Quantity,
        ScanMode Mode,
        Guid? AssoEventsId,
        DateTimeOffset? FairOpeningAt);

    private static DateTimeOffset GetOpeningInstant(AssoEvents assoEvent)
    {
        return assoEvent.HourOpenDoors ?? assoEvent.DateStart;
    }
}
