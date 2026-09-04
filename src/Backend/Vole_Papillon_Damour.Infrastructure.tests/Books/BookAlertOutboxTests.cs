using System.Text.Json;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Vole_Papillon_Damour.Domain.AssociationSettingsAggregate;
using Vole_Papillon_Damour.Domain.AssoEventsAggregate;
using Vole_Papillon_Damour.Domain.AssoEventsAggregate.ValueObjects;
using Vole_Papillon_Damour.Domain.BookAggregate;
using Vole_Papillon_Damour.Domain.BookAggregate.ValueObjects;
using Vole_Papillon_Damour.Domain.BookMovementAggregate;
using Vole_Papillon_Damour.Domain.BookMovementAggregate.ValueObjects;
using Vole_Papillon_Damour.Domain.EventsAggregate.ValueObjects;
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
    public async Task QueueForSession_CarriesEditionDetailsAndFairOpeningIntoDelivery()
    {
        await using var fixture = await BookAlertFixture.CreateAsync();
        var member = await fixture.AddMemberAsync("member@example.org");
        var book = await fixture.AddBookAsync(
            "9782070363735",
            "Titre",
            publisher: "Éditions Test",
            publicationYear: 2020,
            physicalFormat: "Poche");
        var fair = await fixture.AddFairAsync(
            DateTimeOffset.Parse("2026-09-04T18:30:00+00:00"));
        var session = await fixture.AddSessionAsync(
            member.Id,
            ScanMode.NextFair,
            fair.Id);
        await fixture.AddAnnouncementEntryAsync(session, book, member.Id, fair.Id);
        await fixture.AddWatchlistAsync(member.Id, book);

        var outbox = new BookAlertOutbox(fixture.Context);
        await outbox.QueueForSessionAsync(session.Id, ClosedAt, CancellationToken.None);
        await fixture.Context.SaveChangesAsync();
        var message = await fixture.Context.OutboxMessages.SingleAsync();
        message.DueAt = ClosedAt;
        await fixture.Context.SaveChangesAsync();

        var claimed = await outbox.ClaimDueAsync(
            ClosedAt,
            TimeSpan.FromMinutes(5),
            50,
            CancellationToken.None);
        var delivery = await outbox.GetPendingDeliveryAsync(
            claimed[0].MessageId,
            claimed[0].ClaimedUntil,
            ClosedAt,
            CancellationToken.None);

        delivery.Should().NotBeNull();
        var item = delivery!.Items.Should().ContainSingle().Subject;
        item.Publisher.Should().Be("Éditions Test");
        item.PublicationYear.Should().Be(2020);
        item.PhysicalFormat.Should().Be("Poche");
        item.FairOpeningAt.Should().Be(fair.HourOpenDoors);
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

    [Fact]
    public async Task ClaimDue_ReturnsOnlyDuePendingAlertsAndLeasesThem()
    {
        await using var fixture = await BookAlertFixture.CreateAsync();
        var member = await fixture.AddMemberAsync("member@example.org");
        fixture.Context.OutboxMessages.AddRange(
            new OutboxMessage
            {
                Id = Guid.NewGuid(),
                Kind = OutboxMessageKind.AlertEmail,
                PayloadJson = "{\"items\":[{\"isbn13\":\"9782070363735\",\"title\":\"Titre\",\"quantity\":1,\"mode\":\"AvailableNow\"}]}",
                DueAt = ClosedAt,
                Status = OutboxMessageStatus.Pending,
                MemberId = member.Id.Value,
                CreatedAt = ClosedAt
            },
            new OutboxMessage
            {
                Id = Guid.NewGuid(),
                Kind = OutboxMessageKind.AlertEmail,
                PayloadJson = "{\"items\":[]}",
                DueAt = ClosedAt.AddMinutes(1),
                Status = OutboxMessageStatus.Pending,
                MemberId = member.Id.Value,
                CreatedAt = ClosedAt
            });
        await fixture.Context.SaveChangesAsync();
        var outbox = new BookAlertOutbox(fixture.Context);

        var claimed = await outbox.ClaimDueAsync(
            ClosedAt,
            TimeSpan.FromMinutes(5),
            batchSize: 50,
            CancellationToken.None);

        claimed.Should().HaveCount(1);
        claimed[0].MemberId.Should().Be(member.Id.Value);
        claimed[0].Items.Should().ContainSingle();
        claimed[0].ClaimedUntil.Kind.Should().Be(DateTimeKind.Utc);
        var messages = await fixture.Context.OutboxMessages
            .OrderBy(message => message.DueAt)
            .ToListAsync();
        messages[0].Attempts.Should().Be(1);
        messages[0].ClaimedUntil.Should().Be(ClosedAt.AddMinutes(5));
        messages[1].Attempts.Should().Be(0);
    }

    [Fact]
    public async Task GetOldestDueAt_ReturnsTheOldestPendingDueMessageOnly()
    {
        await using var fixture = await BookAlertFixture.CreateAsync();
        var member = await fixture.AddMemberAsync("member@example.org");
        fixture.Context.OutboxMessages.AddRange(
            CreateOutboxMessage(
                Guid.NewGuid(),
                OutboxMessageStatus.Pending,
                null,
                dueAt: ClosedAt.AddMinutes(-20),
                memberId: member.Id.Value),
            CreateOutboxMessage(
                Guid.NewGuid(),
                OutboxMessageStatus.Pending,
                null,
                dueAt: ClosedAt.AddMinutes(5),
                memberId: member.Id.Value),
            CreateOutboxMessage(
                Guid.NewGuid(),
                OutboxMessageStatus.Sent,
                null,
                dueAt: ClosedAt.AddMinutes(-60),
                memberId: member.Id.Value));
        await fixture.Context.SaveChangesAsync();
        var outbox = new BookAlertOutbox(fixture.Context);

        var oldest = await outbox.GetOldestDueAtAsync(
            ClosedAt,
            CancellationToken.None);

        oldest.Should().Be(ClosedAt.AddMinutes(-20));
    }

    [Fact]
    public async Task GetPendingDelivery_RechecksWatchlistAndCooldownBeforeSending()
    {
        await using var fixture = await BookAlertFixture.CreateAsync();
        var member = await fixture.AddMemberAsync("member@example.org");
        var book = await fixture.AddBookAsync("9782070363735", "Titre");
        var session = await fixture.AddSessionAsync(member.Id);
        await fixture.AddDirectEntryAsync(session, book, member.Id);
        await fixture.AddWatchlistAsync(member.Id, book);
        var outbox = new BookAlertOutbox(fixture.Context);
        await outbox.QueueForSessionAsync(session.Id, ClosedAt, CancellationToken.None);
        await fixture.Context.SaveChangesAsync();
        var message = await fixture.Context.OutboxMessages.SingleAsync();
        message.DueAt = ClosedAt;
        await fixture.Context.SaveChangesAsync();

        var claimed = await outbox.ClaimDueAsync(
            ClosedAt,
            TimeSpan.FromMinutes(5),
            50,
            CancellationToken.None);
        var delivery = await outbox.GetPendingDeliveryAsync(
            claimed[0].MessageId,
            claimed[0].ClaimedUntil,
            ClosedAt,
            CancellationToken.None);

        delivery.Should().NotBeNull();
        delivery!.Email.Should().Be("member@example.org");
        delivery.Items.Should().ContainSingle(item => item.Isbn13 == book.Id);

        fixture.Context.UserAlertHistories.Add(UserAlertHistory.Create(
            Guid.NewGuid(),
            member.Id,
            book.Id,
            ClosedAt.AddMinutes(-1)));
        await fixture.Context.SaveChangesAsync();

        var suppressed = await outbox.GetPendingDeliveryAsync(
            claimed[0].MessageId,
            claimed[0].ClaimedUntil,
            ClosedAt,
            CancellationToken.None);

        suppressed.Should().BeNull();
    }

    [Fact]
    public async Task GetPendingDelivery_DropsItemsBelongingToACancelledBookFair()
    {
        await using var fixture = await BookAlertFixture.CreateAsync();
        var member = await fixture.AddMemberAsync("member@example.org");
        var book = await fixture.AddBookAsync("9782070363735", "Titre");
        var fair = await fixture.AddFairAsync();
        var session = await fixture.AddSessionAsync(
            member.Id,
            ScanMode.NextFair,
            fair.Id);
        await fixture.AddAnnouncementEntryAsync(session, book, member.Id, fair.Id);
        await fixture.AddWatchlistAsync(member.Id, book);
        var outbox = new BookAlertOutbox(fixture.Context);
        await outbox.QueueForSessionAsync(session.Id, ClosedAt, CancellationToken.None);
        await fixture.Context.SaveChangesAsync();
        var message = await fixture.Context.OutboxMessages.SingleAsync();
        message.DueAt = ClosedAt;
        await fixture.Context.SaveChangesAsync();

        var claimed = await outbox.ClaimDueAsync(
            ClosedAt,
            TimeSpan.FromMinutes(5),
            50,
            CancellationToken.None);
        fair.Cancel();
        await fixture.Context.SaveChangesAsync();

        var delivery = await outbox.GetPendingDeliveryAsync(
            claimed[0].MessageId,
            claimed[0].ClaimedUntil,
            ClosedAt,
            CancellationToken.None);

        delivery.Should().BeNull();
    }

    [Fact]
    public async Task GetPendingDelivery_WhenLeaseHasBeenReclaimedRejectsTheOldClaim()
    {
        await using var fixture = await BookAlertFixture.CreateAsync();
        var message = CreateOutboxMessage(
            Guid.NewGuid(),
            OutboxMessageStatus.Pending,
            ClosedAt.AddMinutes(11));
        fixture.Context.OutboxMessages.Add(message);
        await fixture.Context.SaveChangesAsync();
        var outbox = new BookAlertOutbox(fixture.Context);

        var delivery = await outbox.GetPendingDeliveryAsync(
            message.Id,
            ClosedAt.AddMinutes(5),
            ClosedAt.AddMinutes(6),
            CancellationToken.None);

        delivery.Should().BeNull();
    }

    [Fact]
    public async Task MarkSent_TransitionsMessageAndWritesOneHistoryPerIsbn()
    {
        await using var fixture = await BookAlertFixture.CreateAsync();
        var member = await fixture.AddMemberAsync("member@example.org");
        var book = await fixture.AddBookAsync("9782070363735", "Titre");
        var message = new OutboxMessage
        {
            Id = Guid.NewGuid(),
            Kind = OutboxMessageKind.AlertEmail,
            PayloadJson = "{\"items\":[]}",
            DueAt = ClosedAt,
            Status = OutboxMessageStatus.Pending,
            MemberId = member.Id.Value,
            CreatedAt = ClosedAt,
            ClaimedUntil = ClosedAt.AddMinutes(5)
        };
        fixture.Context.OutboxMessages.Add(message);
        await fixture.Context.SaveChangesAsync();
        var outbox = new BookAlertOutbox(fixture.Context);

        var first = await outbox.MarkSentAsync(
            message.Id,
            ClosedAt.AddMinutes(5),
            ClosedAt,
            [book.Id, book.Id],
            CancellationToken.None);
        var retry = await outbox.MarkSentAsync(
            message.Id,
            ClosedAt.AddMinutes(5),
            ClosedAt.AddMinutes(1),
            [book.Id],
            CancellationToken.None);

        first.Should().BeTrue();
        retry.Should().BeFalse();
        var persisted = await fixture.Context.OutboxMessages.SingleAsync();
        persisted.Status.Should().Be(OutboxMessageStatus.Sent);
        persisted.SentAt.Should().Be(ClosedAt);
        (await fixture.Context.UserAlertHistories.CountAsync()).Should().Be(1);
        (await fixture.Context.UserAlertHistories.SingleAsync()).OutboxMessageId.Should().Be(message.Id);
    }

    [Fact]
    public async Task MarkSent_WhenLeaseHasExpired_DoesNotStealTheMessage()
    {
        await using var fixture = await BookAlertFixture.CreateAsync();
        var member = await fixture.AddMemberAsync("member@example.org");
        var book = await fixture.AddBookAsync("9782070363735", "Titre");
        var message = new OutboxMessage
        {
            Id = Guid.NewGuid(),
            Kind = OutboxMessageKind.AlertEmail,
            PayloadJson = "{\"items\":[]}",
            DueAt = ClosedAt,
            Status = OutboxMessageStatus.Pending,
            MemberId = member.Id.Value,
            CreatedAt = ClosedAt,
            ClaimedUntil = ClosedAt.AddMinutes(5)
        };
        fixture.Context.OutboxMessages.Add(message);
        await fixture.Context.SaveChangesAsync();
        var outbox = new BookAlertOutbox(fixture.Context);

        var marked = await outbox.MarkSentAsync(
            message.Id,
            ClosedAt.AddMinutes(5),
            ClosedAt.AddMinutes(6),
            [book.Id],
            CancellationToken.None);

        marked.Should().BeFalse();
        var persisted = await fixture.Context.OutboxMessages.SingleAsync();
        persisted.Status.Should().Be(OutboxMessageStatus.Pending);
        persisted.SentAt.Should().BeNull();
        (await fixture.Context.UserAlertHistories.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task MarkSent_WhenLeaseHasBeenReclaimedRejectsTheOldClaim()
    {
        await using var fixture = await BookAlertFixture.CreateAsync();
        var book = await fixture.AddBookAsync("9782070363735", "Titre");
        var message = CreateOutboxMessage(
            Guid.NewGuid(),
            OutboxMessageStatus.Pending,
            ClosedAt.AddMinutes(11));
        fixture.Context.OutboxMessages.Add(message);
        await fixture.Context.SaveChangesAsync();
        var outbox = new BookAlertOutbox(fixture.Context);

        var marked = await outbox.MarkSentAsync(
            message.Id,
            ClosedAt.AddMinutes(5),
            ClosedAt.AddMinutes(6),
            [book.Id],
            CancellationToken.None);

        marked.Should().BeFalse();
        var persisted = await fixture.Context.OutboxMessages.SingleAsync();
        persisted.Status.Should().Be(OutboxMessageStatus.Pending);
        persisted.ClaimedUntil.Should().Be(ClosedAt.AddMinutes(11));
        (await fixture.Context.UserAlertHistories.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task Cancel_WhenLeaseHasBeenReclaimed_DoesNotCancelTheNewClaim()
    {
        await using var fixture = await BookAlertFixture.CreateAsync();
        var message = CreateOutboxMessage(
            Guid.NewGuid(),
            OutboxMessageStatus.Pending,
            ClosedAt.AddMinutes(11));
        fixture.Context.OutboxMessages.Add(message);
        await fixture.Context.SaveChangesAsync();
        var outbox = new BookAlertOutbox(fixture.Context);

        var cancelled = await outbox.CancelAsync(
            message.Id,
            ClosedAt.AddMinutes(5),
            CancellationToken.None);

        cancelled.Should().Be(0);
        var persisted = await fixture.Context.OutboxMessages.SingleAsync();
        persisted.Status.Should().Be(OutboxMessageStatus.Pending);
        persisted.ClaimedUntil.Should().Be(ClosedAt.AddMinutes(11));
    }

    [Fact]
    public async Task RecordFailure_AfterMaximumAttempts_MovesMessageToFailed()
    {
        await using var fixture = await BookAlertFixture.CreateAsync();
        var message = CreateOutboxMessage(
            Guid.NewGuid(),
            OutboxMessageStatus.Pending,
            ClosedAt.AddMinutes(5));
        message.Attempts = 5;
        fixture.Context.OutboxMessages.Add(message);
        await fixture.Context.SaveChangesAsync();
        var outbox = new BookAlertOutbox(fixture.Context);

        await outbox.RecordFailureAsync(
            message.Id,
            ClosedAt.AddMinutes(5),
            "acs-timeout",
            ClosedAt,
            CancellationToken.None);

        var persisted = await fixture.Context.OutboxMessages.SingleAsync();
        persisted.Status.Should().Be(OutboxMessageStatus.Failed);
        persisted.ClaimedUntil.Should().BeNull();
        persisted.LastError.Should().Be("acs-timeout");
    }

    [Fact]
    public async Task RecordFailure_WhenLeaseHasBeenReclaimedRejectsTheOldClaim()
    {
        await using var fixture = await BookAlertFixture.CreateAsync();
        var message = CreateOutboxMessage(
            Guid.NewGuid(),
            OutboxMessageStatus.Pending,
            ClosedAt.AddMinutes(11));
        fixture.Context.OutboxMessages.Add(message);
        await fixture.Context.SaveChangesAsync();
        var outbox = new BookAlertOutbox(fixture.Context);

        await outbox.RecordFailureAsync(
            message.Id,
            ClosedAt.AddMinutes(5),
            "acs-timeout",
            ClosedAt.AddMinutes(6),
            CancellationToken.None);

        var persisted = await fixture.Context.OutboxMessages.SingleAsync();
        persisted.Status.Should().Be(OutboxMessageStatus.Pending);
        persisted.ClaimedUntil.Should().Be(ClosedAt.AddMinutes(11));
        persisted.LastError.Should().BeNull();
    }

    private static OutboxMessage CreateOutboxMessage(
        Guid scanSessionId,
        OutboxMessageStatus status,
        DateTime? claimedUntil,
        DateTime? dueAt = null,
        Guid? memberId = null)
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
            MemberId = memberId ?? Guid.NewGuid(),
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

        public async Task<Book> AddBookAsync(
            string isbn,
            string title,
            string? workId = null,
            string? publisher = null,
            int? publicationYear = null,
            string? physicalFormat = null)
        {
            var book = Book.Create(ParseIsbn(isbn), StartedAt);
            var fields = new List<BookMetadataField> { BookMetadataField.Title };
            if (workId is not null) fields.Add(BookMetadataField.WorkId);
            if (publisher is not null) fields.Add(BookMetadataField.Publisher);
            if (publicationYear is not null) fields.Add(BookMetadataField.PublicationYear);
            if (physicalFormat is not null) fields.Add(BookMetadataField.PhysicalFormat);
            book.ApplyAutomaticMetadata(
                new BookMetadataPatch(
                    title,
                    null,
                    publisher,
                    publicationYear,
                    physicalFormat,
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

        public async Task<AssoEvents> AddFairAsync(DateTimeOffset? hourOpenDoors = null)
        {
            var fair = AssoEvents.Create(
                "Test book fair",
                null,
                new EventsType(EventsType.EventsTypeEnum.Books),
                DateTimeOffset.Parse("2026-09-04T18:00:00+00:00"),
                DateTimeOffset.Parse("2026-09-05T18:00:00+00:00"),
                hourOpenDoors,
                null,
                null,
                new Adresse(null, "Paris", "Rue de test", 75000),
                null,
                [],
                string.Empty);
            Context.AssoEvents.Add(fair);
            await Context.SaveChangesAsync();
            return fair;
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
                watchlist.SuspendAlerts(StartedAt.AddMinutes(1));
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
