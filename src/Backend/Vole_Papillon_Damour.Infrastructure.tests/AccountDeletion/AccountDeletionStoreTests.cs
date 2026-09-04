using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Vole_Papillon_Damour.Application.Common.Interfaces.Persistence;
using Vole_Papillon_Damour.Application.Common.Models;
using Vole_Papillon_Damour.Domain.BookAggregate.ValueObjects;
using Vole_Papillon_Damour.Domain.UserAggregate;
using Vole_Papillon_Damour.Domain.UserAggregate.ValueObjects;
using Vole_Papillon_Damour.Domain.WatchlistAggregate;
using Vole_Papillon_Damour.Infrastructure.AccountDeletion;
using Vole_Papillon_Damour.Infrastructure.Persistence;
using Vole_Papillon_Damour.Infrastructure.Persistence.Outbox;

namespace Vole_Papillon_Damour.Infrastructure.tests.AccountDeletion;

public sealed class AccountDeletionStoreTests
{
    private static readonly DateTime Now = new(2026, 9, 4, 8, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task ClaimPendingAsync_ClaimsOnlyAccountDeletionMessages()
    {
        await using var fixture = await Fixture.CreateAsync();
        var accountDeletionId = Guid.NewGuid();
        var alertId = Guid.NewGuid();

        fixture.Context.OutboxMessages.AddRange(
            new OutboxMessage
            {
                Id = accountDeletionId,
                Kind = OutboxMessageKind.AccountDeletion,
                PayloadJson = "{\"userId\":null,\"externalId\":\"entra-object-id\"}",
                DueAt = Now,
                Status = OutboxMessageStatus.Pending,
                CreatedAt = Now
            },
            new OutboxMessage
            {
                Id = alertId,
                Kind = OutboxMessageKind.AlertEmail,
                PayloadJson = "{\"items\":[]}",
                DueAt = Now,
                Status = OutboxMessageStatus.Pending,
                CreatedAt = Now
            });
        await fixture.Context.SaveChangesAsync();

        var store = new AccountDeletionStore(
            fixture.Context,
            new NoRetainedSalesMovementsPolicy(fixture.Context));

        var claimed = await store.ClaimPendingAsync(
            Now,
            TimeSpan.FromMinutes(5),
            50,
            CancellationToken.None);

        claimed.Should().ContainSingle(item =>
            item.RequestId == accountDeletionId &&
            item.ExternalId == "entra-object-id");
        (await fixture.Context.OutboxMessages.SingleAsync(message => message.Id == alertId))
            .ClaimedUntil
            .Should()
            .BeNull();
    }

    [Fact]
    public async Task EnsurePendingAsync_CreatesAndReusesThePendingMessageOnSqlite()
    {
        await using var fixture = await Fixture.CreateAsync();
        var userId = Guid.NewGuid();
        fixture.Context.Users.Add(User.CreateFromExternalIdentity(
            UserId.Create(userId),
            "entra-object-id",
            "member@example.com",
            Now));
        await fixture.Context.SaveChangesAsync();

        var store = new AccountDeletionStore(
            fixture.Context,
            new NoRetainedSalesMovementsPolicy(fixture.Context));

        var first = await store.EnsurePendingAsync(
            "entra-object-id",
            Now,
            CancellationToken.None);
        var second = await store.EnsurePendingAsync(
            "ENTRA-OBJECT-ID",
            Now.AddMinutes(1),
            CancellationToken.None);

        second.RequestId.Should().Be(first.RequestId);
        second.UserId.Should().Be(userId);
        second.ExternalId.Should().Be("entra-object-id");
        (await fixture.Context.OutboxMessages.CountAsync(message =>
                message.Kind == OutboxMessageKind.AccountDeletion))
            .Should()
            .Be(1);
    }

    [Fact]
    public async Task FinalizeAsync_AnonymizesTheUserAndRemovesAllMemberDataWhenMovementsAreRetained()
    {
        await using var fixture = await Fixture.CreateAsync();
        var userId = Guid.NewGuid();
        var requestId = Guid.NewGuid();
        var alertId = Guid.NewGuid();
        Isbn13.TryCreate("9782070612758", out var isbn13).Should().BeTrue();

        fixture.Context.Users.Add(User.CreateFromExternalIdentity(
            UserId.Create(userId),
            "entra-object-id",
            "member@example.com",
            Now));
        fixture.Context.Watchlists.Add(Watchlist.Create(UserId.Create(userId), Now));
        fixture.Context.WatchlistItems.Add(WatchlistItem.CreateWork(
            Guid.NewGuid(),
            UserId.Create(userId),
            "OL1W",
            Now));
        fixture.Context.UserAlertHistories.Add(UserAlertHistory.Create(
            Guid.NewGuid(),
            UserId.Create(userId),
            isbn13,
            Now,
            alertId));
        fixture.Context.EmailBounceEvents.Add(EmailBounceEvent.Create(
            Guid.NewGuid(),
            "provider-event-1",
            UserId.Create(userId),
            Now));
        fixture.Context.OutboxMessages.AddRange(
            new OutboxMessage
            {
                Id = requestId,
                Kind = OutboxMessageKind.AccountDeletion,
                PayloadJson = $"{{\"userId\":\"{userId}\",\"externalId\":\"entra-object-id\"}}",
                DueAt = Now,
                Status = OutboxMessageStatus.Pending,
                CreatedAt = Now
            },
            new OutboxMessage
            {
                Id = alertId,
                Kind = OutboxMessageKind.AlertEmail,
                MemberId = userId,
                PayloadJson = "{\"items\":[]}",
                DueAt = Now,
                Status = OutboxMessageStatus.Pending,
                CreatedAt = Now
            });
        await fixture.Context.SaveChangesAsync();

        var store = new AccountDeletionStore(fixture.Context, new AlwaysRetainPolicy());

        await store.FinalizeAsync(
            new AccountDeletionWorkItem(requestId, userId, "entra-object-id"),
            Now.AddMinutes(5),
            CancellationToken.None);

        var user = await fixture.Context.Users.SingleAsync();
        user.ExternalId.Should().BeNull();
        user.Email.Should().BeNull();
        user.AnonymizedAt.Should().Be(Now.AddMinutes(5));
        (await fixture.Context.Watchlists.CountAsync()).Should().Be(0);
        (await fixture.Context.WatchlistItems.CountAsync()).Should().Be(0);
        (await fixture.Context.UserAlertHistories.CountAsync()).Should().Be(0);
        (await fixture.Context.EmailBounceEvents.CountAsync()).Should().Be(0);
        (await fixture.Context.OutboxMessages.CountAsync(message =>
                message.Kind == OutboxMessageKind.AlertEmail && message.MemberId == userId))
            .Should()
            .Be(0);
        var deletionMessage = await fixture.Context.OutboxMessages.SingleAsync(
            message => message.Id == requestId);
        deletionMessage.Status.Should().Be(OutboxMessageStatus.Sent);
        deletionMessage.PayloadJson.Should().Be("{}");
    }

    [Fact]
    public async Task FinalizeAsync_DeletesTheUserAndItsAlertOutboxMessagesWhenNoMovementsAreRetained()
    {
        await using var fixture = await Fixture.CreateAsync();
        var userId = Guid.NewGuid();
        var requestId = Guid.NewGuid();
        var alertId = Guid.NewGuid();

        fixture.Context.Users.Add(User.CreateFromExternalIdentity(
            UserId.Create(userId),
            "entra-object-id",
            "member@example.com",
            Now));
        fixture.Context.OutboxMessages.AddRange(
            new OutboxMessage
            {
                Id = requestId,
                Kind = OutboxMessageKind.AccountDeletion,
                PayloadJson = $"{{\"userId\":\"{userId}\",\"externalId\":\"entra-object-id\"}}",
                DueAt = Now,
                Status = OutboxMessageStatus.Pending,
                CreatedAt = Now
            },
            new OutboxMessage
            {
                Id = alertId,
                Kind = OutboxMessageKind.AlertEmail,
                MemberId = userId,
                PayloadJson = "{\"items\":[]}",
                DueAt = Now,
                Status = OutboxMessageStatus.Pending,
                CreatedAt = Now
            });
        await fixture.Context.SaveChangesAsync();

        var store = new AccountDeletionStore(
            fixture.Context,
            new NoRetainedSalesMovementsPolicy(fixture.Context));

        await store.FinalizeAsync(
            new AccountDeletionWorkItem(requestId, userId, "entra-object-id"),
            Now.AddMinutes(5),
            CancellationToken.None);

        (await fixture.Context.Users.CountAsync()).Should().Be(0);
        (await fixture.Context.OutboxMessages.CountAsync(message => message.Id == alertId))
            .Should()
            .Be(0);
        (await fixture.Context.OutboxMessages.CountAsync(message => message.Id == requestId))
            .Should()
            .Be(1);
    }

    private sealed class Fixture : IAsyncDisposable
    {
        private readonly SqliteConnection _connection;

        private Fixture(SqliteConnection connection, ProjectDbContext context)
        {
            _connection = connection;
            Context = context;
        }

        public ProjectDbContext Context { get; }

        public static async Task<Fixture> CreateAsync()
        {
            var connection = new SqliteConnection("Data Source=:memory:");
            await connection.OpenAsync();
            connection.CreateCollation(
                "Latin1_General_100_CI_AI",
                (left, right) => string.Compare(left, right, StringComparison.OrdinalIgnoreCase));
            var options = new DbContextOptionsBuilder<ProjectDbContext>()
                .UseSqlite(connection)
                .Options;
            var context = new TestDbContext(options);
            await context.Database.EnsureCreatedAsync();
            return new Fixture(connection, context);
        }

        public async ValueTask DisposeAsync()
        {
            await Context.DisposeAsync();
            await _connection.DisposeAsync();
        }
    }

    private sealed class AlwaysRetainPolicy : IUserDeletionRetentionPolicy
    {
        public Task<bool> HasRetainedSalesMovementsAsync(
            Guid userId,
            CancellationToken cancellationToken)
        {
            return Task.FromResult(true);
        }
    }

    private sealed class TestDbContext(DbContextOptions<ProjectDbContext> options)
        : ProjectDbContext(options)
    {
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<Vole_Papillon_Damour.Domain.BookAggregate.Book>()
                .Property(book => book.RowVersion)
                .ValueGeneratedNever()
                .IsConcurrencyToken(false);

            foreach (var property in modelBuilder.Model
                         .GetEntityTypes()
                         .SelectMany(entityType => entityType.GetProperties())
                         .Where(property => property.GetColumnType() == "nvarchar(max)"))
            {
                property.SetColumnType("TEXT");
            }
        }
    }
}
