using System.Text.Json;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Vole_Papillon_Damour.Domain.AssociationSettingsAggregate;
using Vole_Papillon_Damour.Domain.BookAggregate;
using Vole_Papillon_Damour.Domain.BookAggregate.ValueObjects;
using Vole_Papillon_Damour.Domain.BookMovementAggregate;
using Vole_Papillon_Damour.Domain.BookMovementAggregate.ValueObjects;
using Vole_Papillon_Damour.Domain.ScanSessionAggregate;
using Vole_Papillon_Damour.Domain.ScanSessionAggregate.ValueObjects;
using Vole_Papillon_Damour.Domain.UserAggregate;
using Vole_Papillon_Damour.Domain.UserAggregate.ValueObjects;
using Vole_Papillon_Damour.Domain.WatchlistAggregate;
using Vole_Papillon_Damour.Infrastructure.Persistence;
using Vole_Papillon_Damour.Infrastructure.Persistence.Outbox;
using Vole_Papillon_Damour.Infrastructure.Services.BookAlerts;

namespace Vole_Papillon_Damour.Infrastructure.tests.Books;

public sealed class BookAlertOutboxTests
{
    private static readonly DateTime StartedAt =
        new(2026, 9, 3, 17, 0, 0, DateTimeKind.Utc);

    private static readonly DateTime ClosedAt =
        new(2026, 9, 3, 18, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task QueueForSession_GroupsMatchingBooksPerMemberAndAppliesConfiguredDelay()
    {
        await using var fixture = await BookAlertFixture.CreateAsync();
        var memberOne = await fixture.AddMemberAsync("one@example.org");
        var memberTwo = await fixture.AddMemberAsync("two@example.org");
        var bookOne = await fixture.AddBookAsync("9782070363735", "Premier titre");
        var bookTwo = await fixture.AddBookAsync("9783140464079", "Deuxième titre");
        var session = await fixture.AddSessionAsync(memberOne.Id);
        await fixture.AddDirectEntryAsync(session, bookOne, memberOne.Id);
        await fixture.AddDirectEntryAsync(session, bookTwo, memberOne.Id);
        await fixture.AddWatchlistAsync(memberOne.Id, bookOne, bookTwo);
        await fixture.AddWatchlistAsync(memberTwo.Id, bookOne);
        await fixture.AddSettingsAsync(memberOne.Id, alertDelayMinutes: 45);

        var outbox = new BookAlertOutbox(fixture.Context);

        await outbox.QueueForSessionAsync(session.Id, ClosedAt, CancellationToken.None);
        await fixture.Context.SaveChangesAsync();

        var messages = await fixture.Context.OutboxMessages
            .OrderBy(message => message.MemberId)
            .ToListAsync();
        messages.Should().HaveCount(2);
        messages.Should().OnlyContain(message =>
            message.Kind == OutboxMessageKind.AlertEmail &&
            message.Status == OutboxMessageStatus.Pending &&
            message.DueAt == ClosedAt.AddMinutes(45) &&
            message.ScanSessionId == session.Id.Value);

        var firstPayload = JsonDocument.Parse(
                messages.Single(message => message.MemberId == memberOne.Id.Value).PayloadJson)
            .RootElement;
        firstPayload.GetProperty("items").GetArrayLength().Should().Be(2);
        firstPayload.GetProperty("items")[0].GetProperty("title").GetString()
            .Should().Be("Premier titre");
        firstPayload.GetProperty("items")[1].GetProperty("title").GetString()
            .Should().Be("Deuxième titre");
    }

    [Fact]
    public async Task QueueForSession_WhenNextFairHasNoDate_DoesNotCreateAnAlert()
    {
        await using var fixture = await BookAlertFixture.CreateAsync();
        var member = await fixture.AddMemberAsync("member@example.org");
        var book = await fixture.AddBookAsync("9782070363735", "Titre");
        var session = await fixture.AddSessionAsync(
            member.Id,
            ScanMode.NextFair,
            targetFairId: null);
        await fixture.AddAnnouncementEntryAsync(session, book, member.Id, assoEventsId: null);
        await fixture.AddWatchlistAsync(member.Id, book);

        var outbox = new BookAlertOutbox(fixture.Context);

        await outbox.QueueForSessionAsync(session.Id, ClosedAt, CancellationToken.None);
        await fixture.Context.SaveChangesAsync();

        (await fixture.Context.OutboxMessages.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task QueueForSession_SkipsSuspendedMembersAndRecentAlertHistory()
    {
        await using var fixture = await BookAlertFixture.CreateAsync();
        var activeMember = await fixture.AddMemberAsync("active@example.org");
        var suspendedMember = await fixture.AddMemberAsync("suspended@example.org");
        var book = await fixture.AddBookAsync("9782070363735", "Titre");
        var session = await fixture.AddSessionAsync(activeMember.Id);
        await fixture.AddDirectEntryAsync(session, book, activeMember.Id);
        await fixture.AddWatchlistAsync(activeMember.Id, book);
        await fixture.AddWatchlistAsync(suspendedMember.Id, book, suspended: true);
        fixture.Context.UserAlertHistories.Add(UserAlertHistory.Create(
            Guid.NewGuid(),
            activeMember.Id,
            book.Id,
            ClosedAt.AddDays(-1)));
        await fixture.Context.SaveChangesAsync();

        var outbox = new BookAlertOutbox(fixture.Context);

        await outbox.QueueForSessionAsync(session.Id, ClosedAt, CancellationToken.None);
        await fixture.Context.SaveChangesAsync();

        (await fixture.Context.OutboxMessages.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task QueueForSession_MatchesAWorkWatchlistItem()
    {
        await using var fixture = await BookAlertFixture.CreateAsync();
        var member = await fixture.AddMemberAsync("member@example.org");
        var book = await fixture.AddBookAsync("9782070363735", "Titre", workId: "work-42");
        var session = await fixture.AddSessionAsync(member.Id);
        await fixture.AddDirectEntryAsync(session, book, member.Id);
        var watchlist = Watchlist.Create(member.Id, StartedAt);
        fixture.Context.Watchlists.Add(watchlist);
        fixture.Context.WatchlistItems.Add(WatchlistItem.CreateWork(
            Guid.NewGuid(),
            member.Id,
            "work-42",
            StartedAt));
        await fixture.Context.SaveChangesAsync();

        var outbox = new BookAlertOutbox(fixture.Context);

        await outbox.QueueForSessionAsync(session.Id, ClosedAt, CancellationToken.None);
        await fixture.Context.SaveChangesAsync();

        (await fixture.Context.OutboxMessages.CountAsync()).Should().Be(1);
    }

    [Fact]
    public async Task CancelPendingForSession_CancelsOnlyPendingAlertMessages()
    {
        await using var fixture = await BookAlertFixture.CreateAsync();
        var member = await fixture.AddMemberAsync("member@example.org");
        var otherMember = await fixture.AddMemberAsync("other@example.org");
        var session = await fixture.AddSessionAsync(member.Id);
        var otherSession = await fixture.AddSessionAsync(otherMember.Id);
        var claimedUntil = ClosedAt.AddMinutes(5);
        fixture.Context.OutboxMessages.AddRange(
            CreateOutboxMessage(session.Id.Value, OutboxMessageStatus.Pending, claimedUntil),
            CreateOutboxMessage(session.Id.Value, OutboxMessageStatus.Sent, null),
            CreateOutboxMessage(session.Id.Value, OutboxMessageStatus.Cancelled, null),
            CreateOutboxMessage(otherSession.Id.Value, OutboxMessageStatus.Pending, null));
        await fixture.Context.SaveChangesAsync();

        var outbox = new BookAlertOutbox(fixture.Context);

        var affected = await outbox.CancelPendingForSessionAsync(
            session.Id,
            CancellationToken.None);
        await fixture.Context.SaveChangesAsync();

        affected.Should().Be(1);
        var messages = await fixture.Context.OutboxMessages.ToListAsync();
        var sessionMessages = messages
            .Where(message => message.ScanSessionId == session.Id.Value)
            .ToArray();
        sessionMessages.Should().HaveCount(3);
        sessionMessages.Count(message => message.Status == OutboxMessageStatus.Cancelled)
            .Should().Be(2);
        sessionMessages.Count(message => message.Status == OutboxMessageStatus.Sent)
            .Should().Be(1);
        sessionMessages.Should().OnlyContain(message =>
            message.Status != OutboxMessageStatus.Pending && message.ClaimedUntil == null);
        messages.Single(message => message.ScanSessionId == otherSession.Id.Value)
            .Status.Should().Be(OutboxMessageStatus.Pending);
    }

    [Fact]
    public async Task ForcePendingForSession_MakesPendingAlertsImmediatelyDueAndReleasesClaims()
    {
        await using var fixture = await BookAlertFixture.CreateAsync();
        var member = await fixture.AddMemberAsync("member@example.org");
        var session = await fixture.AddSessionAsync(member.Id);
        var forcedAt = ClosedAt.AddMinutes(30);
        fixture.Context.OutboxMessages.AddRange(
            CreateOutboxMessage(
                session.Id.Value,
                OutboxMessageStatus.Pending,
                ClosedAt.AddMinutes(5),
                dueAt: ClosedAt.AddHours(2)),
            CreateOutboxMessage(
                session.Id.Value,
                OutboxMessageStatus.Sent,
                null,
                dueAt: ClosedAt.AddHours(2)));
        await fixture.Context.SaveChangesAsync();

        var outbox = new BookAlertOutbox(fixture.Context);

        var affected = await outbox.ForcePendingForSessionAsync(
            session.Id,
            forcedAt,
            CancellationToken.None);
        await fixture.Context.SaveChangesAsync();

        affected.Should().Be(1);
        var messages = await fixture.Context.OutboxMessages
            .OrderBy(message => message.CreatedAt)
            .ToListAsync();
        var pendingMessage = messages.Single(message => message.Status == OutboxMessageStatus.Pending);
        pendingMessage.DueAt.Should().Be(forcedAt);
        pendingMessage.ClaimedUntil.Should().BeNull();
        var sentMessage = messages.Single(message => message.Status == OutboxMessageStatus.Sent);
        sentMessage.DueAt.Should().Be(ClosedAt.AddHours(2));
    }

    private static OutboxMessage CreateOutboxMessage(
        Guid scanSessionId,
        OutboxMessageStatus status,
        DateTime? claimedUntil,
        DateTime? dueAt = null)
    {
        return new OutboxMessage
        {
            Id = Guid.NewGuid(),
            Kind = OutboxMessageKind.AlertEmail,
            PayloadJson = "{\"items\":[]}",
            DueAt = dueAt ?? ClosedAt,
            Status = status,
            Attempts = 0,
            ClaimedUntil = claimedUntil,
            ScanSessionId = scanSessionId,
            MemberId = Guid.NewGuid(),
            CreatedAt = ClosedAt
        };
    }

    private sealed class BookAlertFixture : IAsyncDisposable
    {
        private readonly SqliteConnection _connection;

        private BookAlertFixture(SqliteConnection connection, ProjectDbContext context)
        {
            _connection = connection;
            Context = context;
        }

        public ProjectDbContext Context { get; }

        public static async Task<BookAlertFixture> CreateAsync()
        {
            var connection = new SqliteConnection("Data Source=:memory:");
            await connection.OpenAsync();
            connection.CreateCollation(
                "Latin1_General_100_CI_AI",
                (left, right) => string.Compare(left, right, StringComparison.OrdinalIgnoreCase));
            var options = new DbContextOptionsBuilder<ProjectDbContext>()
                .UseSqlite(connection)
                .Options;
            var context = new BookAlertTestDbContext(options);
            await context.Database.EnsureCreatedAsync();
            return new BookAlertFixture(connection, context);
        }

        public async Task<User> AddMemberAsync(string email)
        {
            var member = User.Create(email, "password", new Name("Prénom", "Nom"), "salt");
            Context.Users.Add(member);
            await Context.SaveChangesAsync();
            return member;
        }

        public async Task<Book> AddBookAsync(string isbn, string title, string? workId = null)
        {
            var book = Book.Create(ParseIsbn(isbn), StartedAt);
            var fields = workId is null
                ? new[] { BookMetadataField.Title }
                : new[] { BookMetadataField.Title, BookMetadataField.WorkId };
            book.ApplyAutomaticMetadata(
                new BookMetadataPatch(
                    title,
                    null,
                    null,
                    null,
                    null,
                    null,
                    null,
                    null,
                    fields,
                    workId),
                BookMetadataSource.OpenLibrary,
                StartedAt,
                rawPayload: null);
            Context.Books.Add(book);
            await Context.SaveChangesAsync();
            return book;
        }

        public async Task<ScanSession> AddSessionAsync(
            UserId volunteerId,
            ScanMode mode = ScanMode.AvailableNow,
            Vole_Papillon_Damour.Domain.EventsAggregate.ValueObjects.AssoEventsId? targetFairId = null)
        {
            var session = ScanSession.Create(volunteerId, mode, targetFairId, StartedAt);
            Context.ScanSessions.Add(session);
            await Context.SaveChangesAsync();
            return session;
        }

        public async Task AddDirectEntryAsync(ScanSession session, Book book, UserId volunteerId)
        {
            book.RecordAvailableEntry(StartedAt.AddMinutes(1));
            Context.BookMovements.Add(BookMovement.Create(
                BookMovementId.CreateUnique(),
                book.Id,
                BookMovementType.DirectEntry,
                1,
                StartedAt.AddMinutes(1),
                StartedAt.AddMinutes(2),
                false,
                session.Id,
                volunteerId,
                null,
                null,
                Guid.NewGuid()));
            await Context.SaveChangesAsync();
        }

        public async Task AddAnnouncementEntryAsync(
            ScanSession session,
            Book book,
            UserId volunteerId,
            Vole_Papillon_Damour.Domain.EventsAggregate.ValueObjects.AssoEventsId? assoEventsId)
        {
            book.RecordAnnouncementEntry(StartedAt.AddMinutes(1));
            Context.BookMovements.Add(BookMovement.Create(
                BookMovementId.CreateUnique(),
                book.Id,
                BookMovementType.AnnouncementEntry,
                1,
                StartedAt.AddMinutes(1),
                StartedAt.AddMinutes(2),
                false,
                session.Id,
                volunteerId,
                assoEventsId,
                null,
                Guid.NewGuid()));
            await Context.SaveChangesAsync();
        }

        public async Task AddWatchlistAsync(UserId userId, Book book, Book? secondBook = null, bool suspended = false)
        {
            var watchlist = Watchlist.Create(userId, StartedAt);
            if (suspended)
            {
                watchlist.SuspendAlerts();
            }

            Context.Watchlists.Add(watchlist);
            Context.WatchlistItems.Add(WatchlistItem.CreateEdition(
                Guid.NewGuid(),
                userId,
                book.Id,
                StartedAt));
            if (secondBook is not null)
            {
                Context.WatchlistItems.Add(WatchlistItem.CreateEdition(
                    Guid.NewGuid(),
                    userId,
                    secondBook.Id,
                    StartedAt));
            }

            await Context.SaveChangesAsync();
        }

        public async Task AddSettingsAsync(UserId updatedBy, int alertDelayMinutes)
        {
            var settings = AssociationSettings.Create(updatedBy, StartedAt);
            settings.Update(
                settings.DuplicateThreshold,
                settings.DemandSalesThreshold,
                settings.DeadStockMinAgeDays,
                settings.DeadStockMinQuantity,
                settings.WatchlistMaxItems,
                settings.AlertCooldownDays,
                settings.SessionIdleTimeoutMinutes,
                alertDelayMinutes,
                updatedBy,
                StartedAt);
            Context.AssociationSettings.Add(settings);
            await Context.SaveChangesAsync();
        }

        private static Isbn13 ParseIsbn(string value)
        {
            return Isbn13.TryCreate(value, out var isbn)
                ? isbn
                : throw new InvalidOperationException($"Invalid test ISBN: {value}");
        }

        public async ValueTask DisposeAsync()
        {
            await Context.DisposeAsync();
            await _connection.DisposeAsync();
        }
    }

    private sealed class BookAlertTestDbContext(DbContextOptions<ProjectDbContext> options)
        : ProjectDbContext(options)
    {
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<Book>()
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
