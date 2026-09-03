using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using Vole_Papillon_Damour.Application.Books.Queries.GetCatalogDelta;
using Vole_Papillon_Damour.Application.Common.Interfaces.Persistence;
using Vole_Papillon_Damour.Application.Common.Interfaces.Services;
using Vole_Papillon_Damour.Domain.AssoEventsAggregate;
using Vole_Papillon_Damour.Domain.AssociationSettingsAggregate;
using Vole_Papillon_Damour.Domain.BookAggregate;
using Vole_Papillon_Damour.Domain.BookAggregate.Entities;
using Vole_Papillon_Damour.Domain.BookAggregate.ValueObjects;
using Vole_Papillon_Damour.Domain.BookMovementAggregate;
using Vole_Papillon_Damour.Domain.OrderAggregate;
using Vole_Papillon_Damour.Domain.ProductAggregate;
using Vole_Papillon_Damour.Domain.ScanSessionAggregate;
using Vole_Papillon_Damour.Domain.ScanSessionAggregate.ValueObjects;
using Vole_Papillon_Damour.Domain.UserAggregate;
using Vole_Papillon_Damour.Domain.UserAggregate.ValueObjects;
using Vole_Papillon_Damour.Domain.WatchlistAggregate;
using Vole_Papillon_Damour.Domain.WatchlistAggregate.ValueObjects;

namespace Vole_Papillon_Damour.Application.tests.Books.Queries;

public sealed class GetCatalogDeltaQueryHandlerTests
{
    internal static readonly DateTime GeneratedAt =
        new(2026, 9, 3, 17, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task Handle_WhenSinceIsProvided_ReturnsChangedBooksAndCurrentSettings()
    {
        await using var fixture = await CatalogDeltaFixture.CreateAsync();
        var unchanged = await fixture.AddBookAsync(
            "9782070363735",
            GeneratedAt.AddMinutes(-10));
        await fixture.AddBookAsync("9783140464079", GeneratedAt.AddMinutes(-1));
        unchanged.Should().NotBeNull();
        await fixture.AddSettingsAsync();

        var handler = fixture.CreateHandler();
        var result = await handler.Handle(
            new GetCatalogDeltaQuery(GeneratedAt.AddMinutes(-5)),
            CancellationToken.None);

        result.IsError.Should().BeFalse();
        result.Value.Books.Should().ContainSingle(book => book.Isbn13 == "9783140464079");
        result.Value.Books.Should().NotContain(book => book.Isbn13 == "9782070363735");
        result.Value.Settings.DuplicateThreshold.Should().Be(7);
        result.Value.NextWatermark.Should().Be(GeneratedAt);
    }

    [Fact]
    public async Task Handle_WhenEditionIsOnAnActiveWatchlist_ProjectsWantedFlag()
    {
        await using var fixture = await CatalogDeltaFixture.CreateAsync();
        var memberId = UserId.Create(Guid.Parse("00000000-0000-0000-0000-000000000010"));
        await fixture.AddBookAsync("9782070363735", GeneratedAt.AddMinutes(-1));
        await fixture.AddWatchlistAsync(memberId, WatchlistAlertStatus.Active);
        fixture.Context.WatchlistItems.Add(
            WatchlistItem.CreateEdition(
                Guid.Parse("00000000-0000-0000-0000-000000000011"),
                memberId,
                ParseIsbn("9782070363735"),
                GeneratedAt.AddMinutes(-1)));
        await fixture.Context.SaveChangesAsync();

        var handler = fixture.CreateHandler();
        var result = await handler.Handle(
            new GetCatalogDeltaQuery(null),
            CancellationToken.None);

        result.IsError.Should().BeFalse();
        result.Value.Books.Single().IsWanted.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_WhenBookIsHiddenAfterWatermark_ReturnsItAsRemoval()
    {
        await using var fixture = await CatalogDeltaFixture.CreateAsync();
        var book = await fixture.AddBookAsync(
            "9782070363735",
            GeneratedAt.AddMinutes(-1));
        book.UpdateCatalogVisibility(true, GeneratedAt);
        await fixture.Context.SaveChangesAsync();

        var handler = fixture.CreateHandler();
        var result = await handler.Handle(
            new GetCatalogDeltaQuery(GeneratedAt.AddMinutes(-5)),
            CancellationToken.None);

        result.IsError.Should().BeFalse();
        result.Value.Books.Single().IsHidden.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_WhenWatchlistStateChangesAfterWatermark_ReprojectsTheVisibleCatalog()
    {
        await using var fixture = await CatalogDeltaFixture.CreateAsync();
        var memberId = UserId.Create(Guid.Parse("00000000-0000-0000-0000-000000000012"));
        await fixture.AddBookAsync("9782070363735", GeneratedAt.AddMinutes(-10));
        await fixture.AddBookAsync("9783140464079", GeneratedAt.AddMinutes(-9));
        var watchlist = Watchlist.Create(memberId, GeneratedAt.AddMinutes(-20));
        watchlist.SuspendAlerts(GeneratedAt.AddMinutes(-8));
        watchlist.ActivateAlerts(GeneratedAt.AddMinutes(-1));
        fixture.Context.Watchlists.Add(watchlist);
        fixture.Context.WatchlistItems.Add(
            WatchlistItem.CreateEdition(
                Guid.Parse("00000000-0000-0000-0000-000000000013"),
                memberId,
                ParseIsbn("9782070363735"),
                GeneratedAt.AddMinutes(-10)));
        await fixture.Context.SaveChangesAsync();

        var handler = fixture.CreateHandler();
        var result = await handler.Handle(
            new GetCatalogDeltaQuery(GeneratedAt.AddMinutes(-5)),
            CancellationToken.None);

        result.IsError.Should().BeFalse();
        result.Value.Books.Should().HaveCount(2);
        result.Value.Books.Single(book => book.Isbn13 == "9782070363735").IsWanted.Should().BeTrue();
    }

    private static Isbn13 ParseIsbn(string value)
    {
        Isbn13.TryCreate(value, out var isbn).Should().BeTrue();
        return isbn;
    }
}

internal sealed class CatalogDeltaFixture : IAsyncDisposable
{
    private readonly SqliteConnection connection;

    private CatalogDeltaFixture(SqliteConnection connection, CatalogDeltaTestDbContext context)
    {
        this.connection = connection;
        Context = context;
    }

    public CatalogDeltaTestDbContext Context { get; }

    public static async Task<CatalogDeltaFixture> CreateAsync()
    {
        var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<CatalogDeltaTestDbContext>()
            .UseSqlite(connection)
            .Options;
        var context = new CatalogDeltaTestDbContext(options);
        await context.Database.EnsureCreatedAsync();
        return new CatalogDeltaFixture(connection, context);
    }

    public async Task<Book> AddBookAsync(string isbn, DateTime updatedAt)
    {
        var book = Book.Create(ParseIsbn(isbn), updatedAt);
        if (updatedAt == GetCatalogDeltaQueryHandlerTests.GeneratedAt.AddMinutes(-1))
        {
            book.RecordAvailableEntry(updatedAt);
        }

        Context.Books.Add(book);
        await Context.SaveChangesAsync();
        return book;
    }

    public async Task AddSettingsAsync()
    {
        var settings = AssociationSettings.Create(
            UserId.Create(Guid.Parse("00000000-0000-0000-0000-000000000001")),
            GetCatalogDeltaQueryHandlerTests.GeneratedAt);
        settings.Update(7, 2, 30, 1, 100, 30, 120, 120, settings.UpdatedBy, GetCatalogDeltaQueryHandlerTests.GeneratedAt);
        Context.AssociationSettings.Add(settings);
        await Context.SaveChangesAsync();
    }

    public async Task AddWatchlistAsync(UserId memberId, WatchlistAlertStatus status)
    {
        var watchlist = Watchlist.Create(memberId, GetCatalogDeltaQueryHandlerTests.GeneratedAt.AddMinutes(-1));
        if (status == WatchlistAlertStatus.Suspended)
        {
            watchlist.SuspendAlerts(GetCatalogDeltaQueryHandlerTests.GeneratedAt.AddMinutes(-1));
        }

        Context.Watchlists.Add(watchlist);
        await Context.SaveChangesAsync();
    }

    public GetCatalogDeltaQueryHandler CreateHandler()
    {
        var clock = Substitute.For<IDateTimeProvider>();
        clock.UtcNow.Returns(GetCatalogDeltaQueryHandlerTests.GeneratedAt);
        return new GetCatalogDeltaQueryHandler(Context, clock);
    }

    public async ValueTask DisposeAsync()
    {
        await Context.DisposeAsync();
        await connection.DisposeAsync();
    }

    private static Isbn13 ParseIsbn(string value)
    {
        Isbn13.TryCreate(value, out var isbn).Should().BeTrue();
        return isbn;
    }
}

internal sealed class CatalogDeltaTestDbContext(DbContextOptions<CatalogDeltaTestDbContext> options)
    : DbContext(options), IProjectDbContext
{
    public DbSet<Book> Books => Set<Book>();
    public DbSet<BookAnnouncement> BookAnnouncements => Set<BookAnnouncement>();
    public DbSet<Watchlist> Watchlists => Set<Watchlist>();
    public DbSet<WatchlistItem> WatchlistItems => Set<WatchlistItem>();
    public DbSet<AssociationSettings> AssociationSettings => Set<AssociationSettings>();

    DbSet<Product> IProjectDbContext.Products => throw new NotSupportedException();
    DbSet<User> IProjectDbContext.Users => throw new NotSupportedException();
    DbSet<AssoEvents> IProjectDbContext.AssoEvents => throw new NotSupportedException();
    DbSet<Order> IProjectDbContext.Orders => throw new NotSupportedException();
    DbSet<BookMovement> IProjectDbContext.BookMovements => throw new NotSupportedException();
    DbSet<ScanSession> IProjectDbContext.ScanSessions => throw new NotSupportedException();
    DbSet<UserAlertHistory> IProjectDbContext.UserAlertHistories => throw new NotSupportedException();
    DbSet<EmailBounceEvent> IProjectDbContext.EmailBounceEvents => throw new NotSupportedException();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Book>(builder =>
        {
            builder.HasKey(book => book.Id);
            builder.Property(book => book.Id)
                .HasColumnName("Isbn13")
                .ValueGeneratedNever()
                .HasConversion(isbn => isbn.Value, value => ParseIsbn(value));
            builder.Ignore(book => book.Isbn13);
            builder.Property(book => book.RedirectedToIsbn13)
                .HasConversion(
                    (Isbn13? isbn) => isbn.HasValue ? isbn.Value.Value : null,
                    (string? value) => value == null ? (Isbn13?)null : ParseIsbn(value));
        });

        modelBuilder.Entity<BookAnnouncement>(builder =>
        {
            builder.HasKey(announcement => announcement.Id);
            builder.Property(announcement => announcement.Id)
                .ValueGeneratedNever()
                .HasConversion(id => id.Value, value => BookAnnouncementId.Create(value));
            builder.Property(announcement => announcement.Isbn13)
                .HasConversion(isbn => isbn.Value, value => ParseIsbn(value));
            builder.Property(announcement => announcement.Status).HasConversion<byte>();
            builder.Property(announcement => announcement.Quantity);
            builder.Ignore(announcement => announcement.AssoEventsId);
            builder.Ignore(announcement => announcement.CreatedAt);
            builder.Ignore(announcement => announcement.ReleasedAt);
            builder.Ignore(announcement => announcement.ScanSessionId);
            builder.Ignore(announcement => announcement.ClientGestureId);
        });

        modelBuilder.Entity<Watchlist>(builder =>
        {
            builder.HasKey(watchlist => watchlist.Id);
            builder.Property(watchlist => watchlist.Id)
                .HasColumnName("UserId")
                .ValueGeneratedNever()
                .HasConversion(id => id.Value, value => UserId.Create(value));
            builder.Property(watchlist => watchlist.AlertStatus).HasConversion<byte>();
            builder.Ignore(watchlist => watchlist.BounceCount);
            builder.Property(watchlist => watchlist.CreatedAt);
            builder.Property(watchlist => watchlist.UpdatedAt);
        });

        modelBuilder.Entity<WatchlistItem>(builder =>
        {
            builder.HasKey(item => item.Id);
            builder.Property(item => item.UserId)
                .HasConversion(id => id.Value, value => UserId.Create(value));
            builder.Property(item => item.Scope).HasConversion<byte>();
            builder.Property(item => item.Isbn13)
                .HasConversion(
                    (Isbn13? isbn) => isbn.HasValue ? isbn.Value.Value : null,
                    (string? value) => value == null ? (Isbn13?)null : ParseIsbn(value));
            builder.Property(item => item.WorkId);
            builder.Property(item => item.AddedAt);
        });

        modelBuilder.Entity<AssociationSettings>(builder =>
        {
            builder.HasKey(settings => settings.Id);
            builder.Property(settings => settings.UpdatedBy)
                .HasConversion(id => id.Value, value => UserId.Create(value));
        });

        modelBuilder.Ignore<Product>();
        modelBuilder.Ignore<User>();
        modelBuilder.Ignore<AssoEvents>();
        modelBuilder.Ignore<Order>();
        modelBuilder.Ignore<BookMovement>();
        modelBuilder.Ignore<ScanSession>();
        modelBuilder.Ignore<UserAlertHistory>();
        modelBuilder.Ignore<EmailBounceEvent>();
    }

    private static Isbn13 ParseIsbn(string value)
    {
        Isbn13.TryCreate(value, out var isbn).Should().BeTrue();
        return isbn;
    }
}
