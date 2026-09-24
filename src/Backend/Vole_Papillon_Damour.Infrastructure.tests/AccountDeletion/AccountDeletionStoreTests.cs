using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Vole_Papillon_Damour.Application.Common.Interfaces.Persistence;
using Vole_Papillon_Damour.Application.Common.Models;
using Vole_Papillon_Damour.Domain.BookAggregate;
using Vole_Papillon_Damour.Domain.BookAggregate.ValueObjects;
using Vole_Papillon_Damour.Domain.BookMovementAggregate;
using Vole_Papillon_Damour.Domain.BookMovementAggregate.ValueObjects;
using Vole_Papillon_Damour.Domain.CheckoutPassageAggregate;
using Vole_Papillon_Damour.Domain.MemberCardAggregate;
using Vole_Papillon_Damour.Domain.MemberSelectionAggregate;
using Vole_Papillon_Damour.Domain.NotFoundReportAggregate;
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
    public async Task FinalizeAsync_RemovesSelectionAndCard_AndAnonymisesPassages()
    {
        await using var fixture = await Fixture.CreateAsync();
        var userId = UserId.Create(Guid.NewGuid());
        var volunteerId = UserId.Create(Guid.NewGuid());
        var otherMemberId = UserId.Create(Guid.NewGuid());
        var requestId = Guid.NewGuid();
        var passageId = Guid.NewGuid();
        var otherPassageId = Guid.NewGuid();
        Isbn13.TryCreate("9782070612758", out var isbn13).Should().BeTrue();
        var member = User.CreateFromExternalIdentity(
            userId,
            "member-to-delete",
            "member@example.com",
            Now);
        fixture.Context.Users.AddRange(
            member,
            User.CreateFromExternalIdentity(
                volunteerId,
                "volunteer-id",
                "volunteer@example.com",
                Now),
            User.CreateFromExternalIdentity(
                otherMemberId,
                "other-member-id",
                "other-member@example.com",
                Now));
        fixture.Context.MemberSelectionItems.Add(MemberSelectionItem.CreateForEdition(
            Guid.NewGuid(), userId, isbn13, Now));
        fixture.Context.MemberCards.Add(MemberCard.Issue(
            Guid.NewGuid(),
            userId,
            $"{MemberCardRecoveryCode.Words[0]}-4271",
            Now));
        var passage = CheckoutPassage.OpenFromSale(passageId, Now, Now);
        passage.Associate(userId, volunteerId, Now);
        fixture.Context.CheckoutPassages.Add(passage);
        var otherPassage = CheckoutPassage.OpenFromSale(otherPassageId, Now, Now);
        otherPassage.Associate(otherMemberId, userId, Now);
        fixture.Context.CheckoutPassages.Add(otherPassage);
        var book = Book.Create(isbn13, Now);
        var movement = BookMovement.Create(
            BookMovementId.CreateUnique(),
            isbn13,
            BookMovementType.Sale,
            -1,
            Now,
            Now,
            clockSuspect: false,
            scanSessionId: null,
            volunteerId: null,
            assoEventsId: null,
            note: null,
            clientGestureId: Guid.NewGuid(),
            checkoutPassageId: passageId);
        fixture.Context.Books.Add(book);
        fixture.Context.BookMovements.Add(movement);
        fixture.Context.CheckoutPassageLines.Add(CheckoutPassageLine.ForOrdinarySale(
            Guid.NewGuid(), passageId, movement, book, isbn13.Value));
        fixture.Context.OutboxMessages.Add(new OutboxMessage
        {
            Id = requestId,
            Kind = OutboxMessageKind.AccountDeletion,
            PayloadJson = $"{{\"userId\":\"{userId.Value}\",\"externalId\":\"member-to-delete\"}}",
            DueAt = Now,
            Status = OutboxMessageStatus.Pending,
            CreatedAt = Now,
        });
        await fixture.Context.SaveChangesAsync();

        var store = new AccountDeletionStore(fixture.Context, new AlwaysRetainPolicy());
        var completedAt = Now.AddMinutes(5);
        await store.FinalizeAsync(
            new AccountDeletionWorkItem(requestId, userId.Value, "member-to-delete"),
            completedAt,
            CancellationToken.None);

        (await fixture.Context.MemberSelectionItems.CountAsync()).Should().Be(0);
        (await fixture.Context.MemberCards.CountAsync()).Should().Be(0);
        var reloaded = await fixture.Context.CheckoutPassages.SingleAsync(candidate => candidate.Id == passageId);
        reloaded.UserId.Should().BeNull();
        reloaded.AssociatedByVolunteerId.Should().BeNull();
        reloaded.Status.Should().Be(Vole_Papillon_Damour.Domain.CheckoutPassageAggregate.ValueObjects.CheckoutPassageStatus.Dissociated);
        reloaded.DissociatedAt.Should().Be(completedAt);
        var otherMemberPassage = await fixture.Context.CheckoutPassages.SingleAsync(candidate => candidate.Id == otherPassageId);
        otherMemberPassage.UserId.Should().Be(otherMemberId);
        otherMemberPassage.AssociatedByVolunteerId.Should().BeNull();
        otherMemberPassage.Status.Should().Be(Vole_Papillon_Damour.Domain.CheckoutPassageAggregate.ValueObjects.CheckoutPassageStatus.Associated);
        (await fixture.Context.CheckoutPassageLines.CountAsync()).Should().Be(1);
        (await fixture.Context.BookMovements.CountAsync()).Should().Be(1);
    }

    [Fact]
    public async Task Delete_KeepsOpenReportsWithoutMemberOrComment()
    {
        await using var fixture = await Fixture.CreateAsync();
        var userId = UserId.Create(Guid.NewGuid());
        var requestId = Guid.NewGuid();
        Isbn13.TryCreate("9782070612758", out var isbn13).Should().BeTrue();

        var openReportId = Guid.NewGuid();
        var closedReportId = Guid.NewGuid();
        var openReport = BookNotFoundReport.CreateForEdition(
            openReportId,
            userId,
            isbn13,
            null,
            "Mon numéro : 06 12 34 56 78",
            Now);
        var closedReport = BookNotFoundReport.CreateForEdition(
            closedReportId,
            userId,
            isbn13,
            null,
            "J'ai aussi cherché près de l'accueil",
            Now);
        closedReport.MarkFound(UserId.Create(Guid.NewGuid()), "Vérifié en rayon", Now.AddMinutes(1));

        fixture.Context.Users.Add(User.CreateFromExternalIdentity(
            userId,
            "member-to-delete",
            "member@example.com",
            Now));
        fixture.Context.BookNotFoundReports.AddRange(openReport, closedReport);
        fixture.Context.OutboxMessages.Add(new OutboxMessage
        {
            Id = requestId,
            Kind = OutboxMessageKind.AccountDeletion,
            PayloadJson = $"{{\"userId\":\"{userId.Value}\",\"externalId\":\"member-to-delete\"}}",
            DueAt = Now,
            Status = OutboxMessageStatus.Pending,
            CreatedAt = Now,
        });
        await fixture.Context.SaveChangesAsync();

        var store = new AccountDeletionStore(
            fixture.Context,
            new NoRetainedSalesMovementsPolicy(fixture.Context));
        await store.FinalizeAsync(
            new AccountDeletionWorkItem(requestId, userId.Value, "member-to-delete"),
            Now.AddMinutes(5),
            CancellationToken.None);

        var openReportAfterDeletion = await fixture.Context.BookNotFoundReports
            .AsNoTracking()
            .SingleAsync(report => report.Id == openReportId);
        openReportAfterDeletion.UserId.Should().BeNull();
        openReportAfterDeletion.Comment.Should().BeNull();

        var closedReportAfterDeletion = await fixture.Context.BookNotFoundReports
            .AsNoTracking()
            .SingleAsync(report => report.Id == closedReportId);
        closedReportAfterDeletion.IsOpen.Should().BeFalse();
        closedReportAfterDeletion.UserId.Should().BeNull();
        closedReportAfterDeletion.Comment.Should().BeNull();
        closedReportAfterDeletion.ClosureNote.Should().Be("Vérifié en rayon");
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
