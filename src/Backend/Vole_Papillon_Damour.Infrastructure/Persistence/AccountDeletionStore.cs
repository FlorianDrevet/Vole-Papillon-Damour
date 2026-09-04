using System.Data;
using System.Data.Common;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using Vole_Papillon_Damour.Application.Common.Interfaces.Persistence;
using Vole_Papillon_Damour.Application.Common.Models;
using Vole_Papillon_Damour.Domain.UserAggregate;
using Vole_Papillon_Damour.Domain.UserAggregate.ValueObjects;
using Vole_Papillon_Damour.Infrastructure.Persistence.Outbox;

namespace Vole_Papillon_Damour.Infrastructure.Persistence;

public sealed class AccountDeletionStore(
    ProjectDbContext dbContext,
    IUserDeletionRetentionPolicy retentionPolicy) : IAccountDeletionStore
{
    private const byte AccountDeletionKind = (byte)OutboxMessageKind.AccountDeletion;
    private const byte PendingStatus = (byte)OutboxMessageStatus.Pending;
    private static readonly JsonSerializerOptions PayloadSerializerOptions = new(JsonSerializerDefaults.Web);

    public async Task<AccountDeletionWorkItem> EnsurePendingAsync(
        string externalId,
        DateTime requestedAt,
        CancellationToken cancellationToken)
    {
        var executionStrategy = dbContext.Database.CreateExecutionStrategy();

        return await executionStrategy.ExecuteAsync(async () =>
        {
            await using var transaction = await dbContext.Database.BeginTransactionAsync(
                IsolationLevel.Serializable,
                cancellationToken);

            var message = await FindPendingAccountDeletionAsync(externalId, cancellationToken);
            if (message is null)
            {
                var user = await dbContext.Users
                    .AsNoTracking()
                    .SingleOrDefaultAsync(candidate => candidate.ExternalId == externalId, cancellationToken);

                message = new OutboxMessage
                {
                    Id = Guid.NewGuid(),
                    Kind = OutboxMessageKind.AccountDeletion,
                    PayloadJson = SerializePayload(new AccountDeletionPayload(
                        user?.Id.Value,
                        externalId)),
                    DueAt = requestedAt,
                    Status = OutboxMessageStatus.Pending,
                    Attempts = 0,
                    CreatedAt = requestedAt
                };

                dbContext.OutboxMessages.Add(message);
                await dbContext.SaveChangesAsync(cancellationToken);
            }

            await transaction.CommitAsync(cancellationToken);
            return ToWorkItem(message);
        });
    }

    public async Task<IReadOnlyList<AccountDeletionWorkItem>> ClaimPendingAsync(
        DateTime now,
        TimeSpan lease,
        int batchSize,
        CancellationToken cancellationToken)
    {
        if (batchSize <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(batchSize));
        }

        // The production database is SQL Server and uses update/read-past locks
        // below. The provider-neutral path keeps the store executable in the
        // SQLite-backed test harness without weakening the SQL Server claim.
        if (!dbContext.Database.IsSqlServer())
        {
            return await ClaimPendingWithProviderQueryAsync(
                now,
                lease,
                batchSize,
                cancellationToken);
        }

        var connection = dbContext.Database.GetDbConnection();
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
                    WHERE [Kind] = @accountDeletionKind
                      AND [Status] = @pendingStatus
                      AND [DueAt] <= @now
                      AND ([ClaimedUntil] IS NULL OR [ClaimedUntil] < @now)
                    ORDER BY [DueAt], [Id]
                )
                UPDATE message
                   SET [ClaimedUntil] = @leaseUntil,
                       [Attempts] = [Attempts] + 1,
                       [LastError] = NULL
                OUTPUT inserted.[Id], inserted.[PayloadJson]
                  FROM [OutboxMessages] AS message
                  INNER JOIN candidates ON candidates.[Id] = message.[Id];
                """;
            AddParameter(command, "@batchSize", batchSize, DbType.Int32);
            AddParameter(command, "@accountDeletionKind", AccountDeletionKind, DbType.Byte);
            AddParameter(command, "@pendingStatus", PendingStatus, DbType.Byte);
            AddParameter(command, "@now", now, DbType.DateTime2);
            AddParameter(command, "@leaseUntil", now.Add(lease), DbType.DateTime2);

            var workItems = new List<AccountDeletionWorkItem>();
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                var requestId = reader.GetGuid(0);
                var payload = reader.GetString(1);
                workItems.Add(ToWorkItem(requestId, payload));
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

    private async Task<IReadOnlyList<AccountDeletionWorkItem>> ClaimPendingWithProviderQueryAsync(
        DateTime now,
        TimeSpan lease,
        int batchSize,
        CancellationToken cancellationToken)
    {
        var messages = await dbContext.OutboxMessages
            .Where(message =>
                message.Kind == OutboxMessageKind.AccountDeletion &&
                message.Status == OutboxMessageStatus.Pending &&
                message.DueAt <= now &&
                (message.ClaimedUntil == null || message.ClaimedUntil < now))
            .OrderBy(message => message.DueAt)
            .ThenBy(message => message.Id)
            .Take(batchSize)
            .ToListAsync(cancellationToken);

        var claimedUntil = now.Add(lease);
        foreach (var message in messages)
        {
            message.ClaimedUntil = claimedUntil;
            message.Attempts++;
            message.LastError = null;
        }

        if (messages.Count > 0)
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        return messages.Select(ToWorkItem).ToList();
    }

    public async Task FinalizeAsync(
        AccountDeletionWorkItem workItem,
        DateTime completedAt,
        CancellationToken cancellationToken)
    {
        var executionStrategy = dbContext.Database.CreateExecutionStrategy();

        await executionStrategy.ExecuteAsync(async () =>
        {
            await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);

            var message = await dbContext.OutboxMessages
                .SingleOrDefaultAsync(candidate => candidate.Id == workItem.RequestId, cancellationToken);

            if (message is null || message.Status != OutboxMessageStatus.Pending)
            {
                await transaction.CommitAsync(cancellationToken);
                return;
            }

            var payload = DeserializePayload(message.PayloadJson);
            var user = await FindUserAsync(payload, cancellationToken);
            if (user is not null)
            {
                var hasRetainedMovements = await retentionPolicy.HasRetainedSalesMovementsAsync(
                    user.Id.Value,
                    cancellationToken);

                if (hasRetainedMovements)
                {
                    user.Anonymize(completedAt);
                }
                else
                {
                    dbContext.Users.Remove(user);
                }
            }

            message.Status = OutboxMessageStatus.Sent;
            message.SentAt = completedAt;
            message.ClaimedUntil = null;
            message.LastError = null;
            // The completed message must not retain the identity it was created to erase.
            message.PayloadJson = "{}";

            await dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        });
    }

    public async Task RecordFailureAsync(
        Guid requestId,
        string failureCode,
        DateTime failedAt,
        CancellationToken cancellationToken)
    {
        var message = await dbContext.OutboxMessages
            .SingleOrDefaultAsync(candidate => candidate.Id == requestId, cancellationToken);

        if (message is null || message.Status != OutboxMessageStatus.Pending)
        {
            return;
        }

        message.Status = OutboxMessageStatus.Pending;
        message.DueAt = failedAt;
        message.ClaimedUntil = null;
        message.LastError = failureCode;
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private async Task<OutboxMessage?> FindPendingAccountDeletionAsync(
        string externalId,
        CancellationToken cancellationToken)
    {
        if (!dbContext.Database.IsSqlServer())
        {
            // JSON_VALUE is SQL Server-specific. Keep the local SQLite/Aspire
            // path provider-neutral; pending account-deletion rows are small
            // and the production provider uses the indexed-kind/status query
            // below with server-side JSON filtering.
            var pendingMessages = await dbContext.OutboxMessages
                .Where(message =>
                    message.Kind == OutboxMessageKind.AccountDeletion &&
                    message.Status == OutboxMessageStatus.Pending)
                .ToListAsync(cancellationToken);

            return pendingMessages.SingleOrDefault(message =>
                string.Equals(
                    DeserializePayload(message.PayloadJson).ExternalId,
                    externalId,
                    StringComparison.OrdinalIgnoreCase));
        }

        return await dbContext.OutboxMessages
            .FromSqlInterpolated($"""
                SELECT *
                  FROM [OutboxMessages]
                 WHERE [Kind] = {AccountDeletionKind}
                   AND [Status] = {PendingStatus}
                   AND JSON_VALUE([PayloadJson], '$.externalId') = {externalId}
                """)
            .SingleOrDefaultAsync(cancellationToken);
    }

    private async Task<User?> FindUserAsync(
        AccountDeletionPayload payload,
        CancellationToken cancellationToken)
    {
        if (payload.UserId is Guid userId)
        {
            var user = await dbContext.Users.SingleOrDefaultAsync(
                candidate => candidate.Id == UserId.Create(userId),
                cancellationToken);

            if (user is not null)
            {
                return user;
            }
        }

        return await dbContext.Users.SingleOrDefaultAsync(
            candidate => candidate.ExternalId == payload.ExternalId,
            cancellationToken);
    }

    private static AccountDeletionWorkItem ToWorkItem(OutboxMessage message)
    {
        return ToWorkItem(message.Id, message.PayloadJson);
    }

    private static AccountDeletionWorkItem ToWorkItem(Guid requestId, string payloadJson)
    {
        var payload = DeserializePayload(payloadJson);
        return new AccountDeletionWorkItem(requestId, payload.UserId, payload.ExternalId);
    }

    private static string SerializePayload(AccountDeletionPayload payload)
    {
        return JsonSerializer.Serialize(payload, PayloadSerializerOptions);
    }

    private static AccountDeletionPayload DeserializePayload(string payloadJson)
    {
        return JsonSerializer.Deserialize<AccountDeletionPayload>(payloadJson, PayloadSerializerOptions)
            ?? throw new InvalidOperationException("The account deletion outbox payload is invalid.");
    }

    private static void AddParameter(DbCommand command, string name, object value, DbType type)
    {
        var parameter = command.CreateParameter();
        parameter.ParameterName = name;
        parameter.DbType = type;
        parameter.Value = value;
        command.Parameters.Add(parameter);
    }

    private sealed record AccountDeletionPayload(
        [property: JsonPropertyName("userId")] Guid? UserId,
        [property: JsonPropertyName("externalId")] string ExternalId);
}
