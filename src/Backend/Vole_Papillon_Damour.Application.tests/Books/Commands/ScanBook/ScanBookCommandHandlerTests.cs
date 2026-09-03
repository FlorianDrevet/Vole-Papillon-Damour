using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using Vole_Papillon_Damour.Application.Books.Commands.ScanBook;
using Vole_Papillon_Damour.Application.Books.Commands.RegisterSale;
using Vole_Papillon_Damour.Application.Books.Commands.VoidSale;
using Vole_Papillon_Damour.Application.Books.Commands.AdjustQuantity;
using Vole_Papillon_Damour.Application.Books.Commands.AssociationSettings;
using Vole_Papillon_Damour.Application.Books.Queries.GetAssociationSettings;
using Vole_Papillon_Damour.Application.Books.Commands.AttachUndatedAnnouncements;
using Vole_Papillon_Damour.Application.Books.Commands.BookFlags;
using Vole_Papillon_Damour.Application.Books.Commands.ReassignSessionMode;
using Vole_Papillon_Damour.Application.Books.Commands.ScanSession;
using Vole_Papillon_Damour.Application.Common.Interfaces.Persistence;
using Vole_Papillon_Damour.Application.Common.Interfaces.Services;
using Vole_Papillon_Damour.Domain.AssoEventsAggregate;
using Vole_Papillon_Damour.Domain.AssoEventsAggregate.ValueObjects;
using Vole_Papillon_Damour.Domain.BookAggregate;
using Vole_Papillon_Damour.Domain.BookAggregate.Entities;
using Vole_Papillon_Damour.Domain.BookAggregate.ValueObjects;
using Vole_Papillon_Damour.Domain.BookMovementAggregate;
using Vole_Papillon_Damour.Domain.BookMovementAggregate.ValueObjects;
using Vole_Papillon_Damour.Domain.EventsAggregate.ValueObjects;
using Vole_Papillon_Damour.Domain.OrderAggregate;
using Vole_Papillon_Damour.Domain.ProductAggregate;
using Vole_Papillon_Damour.Domain.ScanSessionAggregate.ValueObjects;
using Vole_Papillon_Damour.Domain.UserAggregate;
using Vole_Papillon_Damour.Domain.UserAggregate.ValueObjects;
using Vole_Papillon_Damour.Domain.WatchlistAggregate;
using DomainScanSession = Vole_Papillon_Damour.Domain.ScanSessionAggregate.ScanSession;
using AssociationSettingsEntity = Vole_Papillon_Damour.Domain.AssociationSettingsAggregate.AssociationSettings;

namespace Vole_Papillon_Damour.Application.tests.Books.Commands.ScanBook;

public sealed class ScanBookCommandHandlerTests
{
    internal static readonly DateTime SessionStartedAt = new(2026, 9, 3, 17, 0, 0, DateTimeKind.Utc);
    internal static readonly DateTime ClientScanAt = new(2026, 9, 3, 17, 1, 0, DateTimeKind.Utc);
    internal static readonly DateTime ReceivedAt = new(2026, 9, 3, 17, 2, 0, DateTimeKind.Utc);

    [Fact]
    public async Task Handle_WhenAvailableScanIsKept_CreatesBookAndDirectEntry()
    {
        await using var fixture = await ScanBookFixture.CreateAsync();
        var session = await fixture.AddSessionAsync(ScanMode.AvailableNow);
        var handler = fixture.CreateHandler();

        var result = await handler.Handle(
            CreateCommand(session, "978-2-07-036373-5", kept: true),
            CancellationToken.None);

        result.IsError.Should().BeFalse();
        result.Value.Isbn13.Should().Be("9782070363735");
        result.Value.Verdict.Verdict.Should().Be(BookVerdict.FirstCopy);
        result.Value.QuantityAvailable.Should().Be(1);
        result.Value.QuantityAnnounced.Should().Be(0);
        result.Value.MovementType.Should().Be(BookMovementType.DirectEntry);
        result.Value.AlreadyProcessed.Should().BeFalse();

        var book = await fixture.Context.Books.SingleAsync();
        book.QuantityAvailable.Should().Be(1);
        (await fixture.Context.BookMovements.SingleAsync()).Quantity.Should().Be(1);
        (await fixture.Context.BookAnnouncements.CountAsync()).Should().Be(0);

        var persistedSession = await fixture.Context.ScanSessions.SingleAsync();
        persistedSession.ScannedCount.Should().Be(1);
        persistedSession.KeptCount.Should().Be(1);
        (await fixture.Context.AssociationSettings.SingleAsync()).DuplicateThreshold.Should().Be(5);
    }

    [Fact]
    public async Task Handle_WhenScannedIsbnIsRedirected_WritesTheCanonicalBook()
    {
        await using var fixture = await ScanBookFixture.CreateAsync();
        var session = await fixture.AddSessionAsync(ScanMode.AvailableNow);
        var source = Book.Create(ParseIsbn("9782070363735"), ScanBookCommandHandlerTests.SessionStartedAt);
        var canonical = Book.Create(ParseIsbn("9783140464079"), ScanBookCommandHandlerTests.SessionStartedAt);
        source.RedirectTo(canonical.Id);
        fixture.Context.Books.AddRange(source, canonical);
        await fixture.Context.SaveChangesAsync();
        var handler = fixture.CreateHandler();

        var result = await handler.Handle(
            CreateCommand(session, "9782070363735", kept: true),
            CancellationToken.None);

        result.IsError.Should().BeFalse();
        result.Value.Isbn13.Should().Be("9783140464079");
        (await fixture.Context.Books.SingleAsync(book => book.Id == canonical.Id))
            .QuantityAvailable.Should().Be(1);
        (await fixture.Context.Books.SingleAsync(book => book.Id == source.Id))
            .QuantityAvailable.Should().Be(0);
        (await fixture.Context.BookMovements.SingleAsync()).Isbn13.Should().Be(canonical.Id);
    }

    [Fact]
    public async Task Handle_WhenNextFairScanIsKept_CreatesAnnouncementAndDoesNotIncreaseAvailableStock()
    {
        await using var fixture = await ScanBookFixture.CreateAsync();
        var targetFairId = AssoEventsId.CreateUnique();
        var session = await fixture.AddSessionAsync(ScanMode.NextFair, targetFairId);
        var handler = fixture.CreateHandler();
        var command = CreateCommand(session, "9782070363735", kept: true);

        var result = await handler.Handle(command, CancellationToken.None);

        result.IsError.Should().BeFalse();
        result.Value.MovementType.Should().Be(BookMovementType.AnnouncementEntry);
        result.Value.QuantityAvailable.Should().Be(0);
        result.Value.QuantityAnnounced.Should().Be(1);

        var announcement = await fixture.Context.BookAnnouncements.SingleAsync();
        announcement.Isbn13.Value.Should().Be("9782070363735");
        announcement.AssoEventsId.Should().Be(targetFairId);
        announcement.Status.Should().Be(BookAnnouncementStatus.Announced);
        announcement.Quantity.Should().Be(1);
        announcement.ClientGestureId.Should().Be(command.ClientGestureId);

        var movement = await fixture.Context.BookMovements.SingleAsync();
        movement.Type.Should().Be(BookMovementType.AnnouncementEntry);
        movement.AssoEventsId.Should().Be(targetFairId);
    }

    [Fact]
    public async Task Handle_WhenScanIsRejected_CreatesRejectionMovementWithoutAddingStock()
    {
        await using var fixture = await ScanBookFixture.CreateAsync();
        var session = await fixture.AddSessionAsync(ScanMode.AvailableNow);
        var handler = fixture.CreateHandler();

        var result = await handler.Handle(
            CreateCommand(session, "9782070363735", kept: false),
            CancellationToken.None);

        result.IsError.Should().BeFalse();
        result.Value.MovementType.Should().Be(BookMovementType.Rejection);
        result.Value.QuantityAvailable.Should().Be(0);

        var book = await fixture.Context.Books.SingleAsync();
        book.QuantityAvailable.Should().Be(0);
        book.RejectionCount.Should().Be(1);
        (await fixture.Context.BookMovements.SingleAsync()).Quantity.Should().Be(1);
        (await fixture.Context.ScanSessions.SingleAsync()).RejectedCount.Should().Be(1);
    }

    [Fact]
    public async Task Handle_WhenGestureIsRetried_IsIdempotent()
    {
        await using var fixture = await ScanBookFixture.CreateAsync();
        var session = await fixture.AddSessionAsync(ScanMode.AvailableNow);
        var handler = fixture.CreateHandler();
        var command = CreateCommand(session, "9782070363735", kept: true);

        var firstResult = await handler.Handle(command, CancellationToken.None);
        var retryResult = await handler.Handle(command, CancellationToken.None);

        firstResult.IsError.Should().BeFalse();
        retryResult.IsError.Should().BeFalse();
        retryResult.Value.AlreadyProcessed.Should().BeTrue();
        (await fixture.Context.Books.SingleAsync()).QuantityAvailable.Should().Be(1);
        (await fixture.Context.BookMovements.CountAsync()).Should().Be(1);
        (await fixture.Context.ScanSessions.SingleAsync()).ScannedCount.Should().Be(1);
        (await fixture.Context.BookAnnouncements.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task Handle_WhenClientClockIsBeforeSession_UsesServerTimeAndMarksClockSuspect()
    {
        await using var fixture = await ScanBookFixture.CreateAsync();
        var session = await fixture.AddSessionAsync(ScanMode.AvailableNow);
        var handler = fixture.CreateHandler();
        var command = CreateCommand(
            session,
            "9782070363735",
            kept: true,
            occurredAt: SessionStartedAt.AddMinutes(-1));

        var result = await handler.Handle(command, CancellationToken.None);

        result.IsError.Should().BeFalse();
        result.Value.ClockSuspect.Should().BeTrue();
        var movement = await fixture.Context.BookMovements.SingleAsync();
        movement.OccurredAt.Should().Be(ReceivedAt);
        movement.ReceivedAt.Should().Be(ReceivedAt);
        movement.ClockSuspect.Should().BeTrue();
        (await fixture.Context.ScanSessions.SingleAsync()).LastScanAt.Should().Be(ReceivedAt);
    }

    [Fact]
    public async Task Handle_WhenIsbnIsInvalid_ReturnsValidationErrorWithoutPersisting()
    {
        await using var fixture = await ScanBookFixture.CreateAsync();
        var session = await fixture.AddSessionAsync(ScanMode.AvailableNow);
        var handler = fixture.CreateHandler();

        var result = await handler.Handle(
            CreateCommand(session, "not-an-isbn", kept: true),
            CancellationToken.None);

        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("Book.InvalidIsbn");
        (await fixture.Context.Books.CountAsync()).Should().Be(0);
        (await fixture.Context.BookMovements.CountAsync()).Should().Be(0);
    }

    private static ScanBookCommand CreateCommand(
        DomainScanSession session,
        string isbn,
        bool kept,
        DateTime? occurredAt = null,
        Guid? clientGestureId = null)
    {
        return new ScanBookCommand(
            session.Id,
            isbn,
            kept,
            occurredAt ?? ClientScanAt,
            clientGestureId ?? Guid.NewGuid());
    }

    private static Isbn13 ParseIsbn(string value)
    {
        Isbn13.TryCreate(value, out var isbn).Should().BeTrue();
        return isbn;
    }
}

internal sealed class ScanBookFixture : IAsyncDisposable
{
    private readonly SqliteConnection _connection;

    private ScanBookFixture(SqliteConnection connection, ScanBookTestDbContext context)
    {
        _connection = connection;
        Context = context;
    }

    public ScanBookTestDbContext Context { get; }
    public IBookAlertOutbox AlertOutbox { get; } = Substitute.For<IBookAlertOutbox>();

    public static async Task<ScanBookFixture> CreateAsync()
    {
        var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();

        var options = new DbContextOptionsBuilder<ScanBookTestDbContext>()
            .UseSqlite(connection)
            .Options;
        var context = new ScanBookTestDbContext(options);
        await context.Database.EnsureCreatedAsync();

        return new ScanBookFixture(connection, context);
    }

    public async Task<DomainScanSession> AddSessionAsync(
        ScanMode mode,
        AssoEventsId? targetFairId = null,
        UserId? volunteerId = null)
    {
        var session = DomainScanSession.Create(
            volunteerId ?? UserId.Create(Guid.Parse("00000000-0000-0000-0000-000000000001")),
            mode,
            targetFairId,
            ScanBookCommandHandlerTests.SessionStartedAt);

        Context.ScanSessions.Add(session);
        await Context.SaveChangesAsync();
        return session;
    }

    public async Task<Book> AddBookAsync(string isbn, int quantityAvailable)
    {
        var book = Book.Create(ParseIsbn(isbn), ScanBookCommandHandlerTests.SessionStartedAt);
        for (var index = 0; index < quantityAvailable; index++)
        {
            book.RecordAvailableEntry(ScanBookCommandHandlerTests.ClientScanAt);
        }

        Context.Books.Add(book);
        await Context.SaveChangesAsync();
        return book;
    }

    public async Task<AssoEvents> AddFairAsync(
        DateTimeOffset dateStart,
        DateTimeOffset? dateEnd,
        DateTimeOffset? hourOpenDoors,
        DateTimeOffset? hourCloseDoors)
    {
        var fair = AssoEvents.Create(
            "Test book fair",
            null,
            new EventsType(EventsType.EventsTypeEnum.Books),
            dateStart,
            dateEnd,
            hourOpenDoors,
            hourCloseDoors,
            null,
            new Adresse(null, "Paris", "Rue de test", 75000),
            null,
            [],
            string.Empty);
        Context.AssoEvents.Add(fair);
        await Context.SaveChangesAsync();
        return fair;
    }

    public async Task<AssoEventsId> AddOtherEventAsync()
    {
        var otherEvent = AssoEvents.Create(
            "Test other event",
            null,
            new EventsType(EventsType.EventsTypeEnum.Other),
            DateTimeOffset.Parse("2026-09-03T00:00:00+02:00"),
            DateTimeOffset.Parse("2026-09-04T00:00:00+02:00"),
            null,
            null,
            null,
            new Adresse(null, "Paris", "Rue de test", 75000),
            null,
            [],
            string.Empty);
        Context.AssoEvents.Add(otherEvent);
        await Context.SaveChangesAsync();
        return otherEvent.Id;
    }

    public ScanBookCommandHandler CreateHandler()
    {
        var clock = Substitute.For<IDateTimeProvider>();
        clock.UtcNow.Returns(ScanBookCommandHandlerTests.ReceivedAt);
        return new ScanBookCommandHandler(Context, clock);
    }

    public RegisterSaleCommandHandler CreateRegisterSaleHandler()
    {
        var clock = Substitute.For<IDateTimeProvider>();
        clock.UtcNow.Returns(_receivedAt);
        return new RegisterSaleCommandHandler(Context, clock);
    }

    public VoidSaleCommandHandler CreateVoidSaleHandler()
    {
        var clock = Substitute.For<IDateTimeProvider>();
        clock.UtcNow.Returns(_receivedAt);
        return new VoidSaleCommandHandler(Context, clock);
    }

    public AdjustQuantityCommandHandler CreateAdjustQuantityHandler()
    {
        var clock = Substitute.For<IDateTimeProvider>();
        clock.UtcNow.Returns(_receivedAt);
        return new AdjustQuantityCommandHandler(Context, clock);
    }

    public UpdateAssociationSettingsCommandHandler CreateUpdateAssociationSettingsHandler()
    {
        var clock = Substitute.For<IDateTimeProvider>();
        clock.UtcNow.Returns(_receivedAt);
        return new UpdateAssociationSettingsCommandHandler(Context, clock);
    }

    public GetAssociationSettingsQueryHandler CreateGetAssociationSettingsHandler()
    {
        return new GetAssociationSettingsQueryHandler(Context);
    }

    public AttachUndatedAnnouncementsCommandHandler CreateAttachUndatedAnnouncementsHandler()
    {
        return new AttachUndatedAnnouncementsCommandHandler(Context);
    }

    public MarkBookRareCommandHandler CreateMarkBookRareHandler()
    {
        var clock = Substitute.For<IDateTimeProvider>();
        clock.UtcNow.Returns(_receivedAt);
        return new MarkBookRareCommandHandler(Context, clock);
    }

    public HideBookCommandHandler CreateHideBookHandler()
    {
        var clock = Substitute.For<IDateTimeProvider>();
        clock.UtcNow.Returns(_receivedAt);
        return new HideBookCommandHandler(Context, clock);
    }

    public CloseScanSessionCommandHandler CreateCloseScanSessionHandler()
    {
        var clock = Substitute.For<IDateTimeProvider>();
        clock.UtcNow.Returns(_receivedAt);
        return new CloseScanSessionCommandHandler(Context, clock, AlertOutbox);
    }

    public ReassignSessionModeCommandHandler CreateReassignSessionModeHandler()
    {
        var clock = Substitute.For<IDateTimeProvider>();
        clock.UtcNow.Returns(_receivedAt);
        return new ReassignSessionModeCommandHandler(Context, clock, AlertOutbox);
    }

    public void SetReceivedAt(DateTime receivedAt)
    {
        _receivedAt = receivedAt;
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

    private DateTime _receivedAt = ScanBookCommandHandlerTests.ReceivedAt;
}

internal sealed class ScanBookTestDbContext(DbContextOptions<ScanBookTestDbContext> options)
    : DbContext(options), IProjectDbContext
{
    public DbSet<Book> Books => Set<Book>();
    public DbSet<BookAnnouncement> BookAnnouncements => Set<BookAnnouncement>();
    public DbSet<BookMovement> BookMovements => Set<BookMovement>();
    public DbSet<DomainScanSession> ScanSessions => Set<DomainScanSession>();
    public DbSet<AssociationSettingsEntity> AssociationSettings => Set<AssociationSettingsEntity>();
    public DbSet<AssoEvents> AssoEvents => Set<AssoEvents>();
    public DbSet<Watchlist> Watchlists => Set<Watchlist>();
    public DbSet<WatchlistItem> WatchlistItems => Set<WatchlistItem>();
    public DbSet<UserAlertHistory> UserAlertHistories => Set<UserAlertHistory>();

    DbSet<EmailBounceEvent> IProjectDbContext.EmailBounceEvents => throw new NotSupportedException();

    DbSet<Product> IProjectDbContext.Products => throw new NotSupportedException();
    DbSet<User> IProjectDbContext.Users => throw new NotSupportedException();
    DbSet<AssoEvents> IProjectDbContext.AssoEvents => AssoEvents;
    DbSet<Order> IProjectDbContext.Orders => throw new NotSupportedException();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Ignore<Product>();
        modelBuilder.Ignore<User>();
        modelBuilder.Ignore<Order>();
        modelBuilder.Ignore<EmailBounceEvent>();
        modelBuilder.Ignore<Watchlist>();
        modelBuilder.Ignore<WatchlistItem>();
        modelBuilder.Ignore<UserAlertHistory>();

        modelBuilder.Entity<AssoEvents>(builder =>
        {
            builder.HasKey(assoEvent => assoEvent.Id);
            builder.Property(assoEvent => assoEvent.Id)
                .ValueGeneratedNever()
                .HasConversion(id => id.Value, value => AssoEventsId.Create(value));
            builder.Property(assoEvent => assoEvent.EventsType)
                .HasConversion(
                    type => (int)type.Value,
                    value => new EventsType((EventsType.EventsTypeEnum)value));
            builder.Ignore(assoEvent => assoEvent.UrlImage);
            builder.Ignore(assoEvent => assoEvent.UrlRegistration);
            builder.Ignore(assoEvent => assoEvent.UrlImageMap);
            builder.Ignore(assoEvent => assoEvent.Adresse);
            builder.Ignore(assoEvent => assoEvent.Description);
            builder.Ignore(assoEvent => assoEvent.BingoHasBeenWon);
            builder.Ignore(assoEvent => assoEvent.CurrentPartieIndex);
            builder.Ignore(assoEvent => assoEvent.Parties);
            builder.Ignore(assoEvent => assoEvent.BingoNumeros);
        });

        modelBuilder.Entity<Book>(builder =>
        {
            builder.HasKey(book => book.Id);
            builder.Ignore(book => book.Isbn13);
            builder.Property(book => book.Id)
                .ValueGeneratedNever()
                .HasConversion(isbn => isbn.Value, value => ParseIsbn(value));
            builder.Property(book => book.RedirectedToIsbn13)
                .HasConversion(
                    (Isbn13? isbn) => isbn.HasValue ? isbn.Value.Value : null,
                    (string? value) => value == null ? (Isbn13?)null : ParseIsbn(value));
            builder.Property(book => book.MetadataStatus).HasConversion<byte>();
            builder.Property(book => book.MetadataSource).HasConversion<byte>();
        });

        modelBuilder.Entity<BookAnnouncement>(builder =>
        {
            builder.HasKey(announcement => announcement.Id);
            builder.Property(announcement => announcement.Id)
                .ValueGeneratedNever()
                .HasConversion(id => id.Value, value => BookAnnouncementId.Create(value));
            builder.Property(announcement => announcement.Isbn13)
                .HasConversion(isbn => isbn.Value, value => ParseIsbn(value));
            builder.Property(announcement => announcement.AssoEventsId)
                .HasConversion(
                    (AssoEventsId? id) => id == null ? (Guid?)null : id.Value,
                    (Guid? value) => value.HasValue ? AssoEventsId.Create(value.Value) : null);
            builder.Property(announcement => announcement.ScanSessionId)
                .HasConversion(id => id.Value, value => ScanSessionId.Create(value));
            builder.Property(announcement => announcement.Status).HasConversion<byte>();
        });

        modelBuilder.Entity<BookMovement>(builder =>
        {
            builder.HasKey(movement => movement.Id);
            builder.Property(movement => movement.Id)
                .ValueGeneratedNever()
                .HasConversion(id => id.Value, value => BookMovementId.Create(value));
            builder.Property(movement => movement.Isbn13)
                .HasConversion(isbn => isbn.Value, value => ParseIsbn(value));
            builder.Property(movement => movement.Type).HasConversion<byte>();
            builder.Property(movement => movement.ScanSessionId)
                .HasConversion(
                    (ScanSessionId? id) => id == null ? (Guid?)null : id.Value,
                    (Guid? value) => value.HasValue ? ScanSessionId.Create(value.Value) : null);
            builder.Property(movement => movement.VolunteerId)
                .HasConversion(
                    (UserId? id) => id == null ? (Guid?)null : id.Value,
                    (Guid? value) => value.HasValue ? UserId.Create(value.Value) : null);
            builder.Property(movement => movement.AssoEventsId)
                .HasConversion(
                    (AssoEventsId? id) => id == null ? (Guid?)null : id.Value,
                    (Guid? value) => value.HasValue ? AssoEventsId.Create(value.Value) : null);
            builder.Property(movement => movement.ReversalOfMovementId)
                .HasConversion(
                    (BookMovementId? id) => id == null ? (Guid?)null : id.Value,
                    (Guid? value) => value.HasValue ? BookMovementId.Create(value.Value) : null);
            builder.HasIndex(movement => movement.ClientGestureId).IsUnique();
            builder.HasIndex(movement => movement.ReversalOfMovementId).IsUnique();
        });

        modelBuilder.Entity<DomainScanSession>(builder =>
        {
            builder.HasKey(session => session.Id);
            builder.Property(session => session.Id)
                .ValueGeneratedNever()
                .HasConversion(id => id.Value, value => ScanSessionId.Create(value));
            builder.Property(session => session.VolunteerId)
                .HasConversion(id => id.Value, value => UserId.Create(value));
            builder.Property(session => session.TargetAssoEventsId)
                .HasConversion(
                    (AssoEventsId? id) => id == null ? (Guid?)null : id.Value,
                    (Guid? value) => value.HasValue ? AssoEventsId.Create(value.Value) : null);
            builder.Property(session => session.Mode).HasConversion<byte>();
            builder.Property(session => session.CloseReason).HasConversion<byte>();
            builder.Property(session => session.Status).HasConversion<byte>();
        });

        modelBuilder.Entity<AssociationSettingsEntity>(builder =>
        {
            builder.HasKey(settings => settings.Id);
            builder.Property(settings => settings.UpdatedBy)
                .HasConversion(id => id.Value, value => UserId.Create(value));
        });
    }

    private static Isbn13 ParseIsbn(string value)
    {
        return Isbn13.TryCreate(value, out var isbn)
            ? isbn
            : throw new InvalidOperationException($"Invalid test ISBN: {value}");
    }
}
