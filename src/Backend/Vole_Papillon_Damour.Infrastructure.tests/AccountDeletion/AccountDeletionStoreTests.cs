using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Vole_Papillon_Damour.Domain.UserAggregate;
using Vole_Papillon_Damour.Domain.UserAggregate.ValueObjects;
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
