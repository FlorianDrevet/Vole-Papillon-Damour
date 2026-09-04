using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using FluentAssertions;
using Vole_Papillon_Damour.Application.Common.Interfaces.Persistence;
using Vole_Papillon_Damour.Application.Common.Interfaces.Services;
using Vole_Papillon_Damour.Application.WatchlistFeature.Common;
using Vole_Papillon_Damour.Domain.AssoEventsAggregate;
using Vole_Papillon_Damour.Domain.AssoEventsAggregate.ValueObjects;
using Vole_Papillon_Damour.Domain.AssociationSettingsAggregate;
using Vole_Papillon_Damour.Domain.BookAggregate;
using Vole_Papillon_Damour.Domain.BookAggregate.Entities;
using Vole_Papillon_Damour.Domain.BookAggregate.ValueObjects;
using Vole_Papillon_Damour.Domain.BookMovementAggregate;
using Vole_Papillon_Damour.Domain.EventsAggregate.ValueObjects;
using Vole_Papillon_Damour.Domain.OrderAggregate;
using Vole_Papillon_Damour.Domain.ProductAggregate;
using Vole_Papillon_Damour.Domain.ScanSessionAggregate;
using Vole_Papillon_Damour.Domain.ScanSessionAggregate.ValueObjects;
using Vole_Papillon_Damour.Domain.UserAggregate;
using Vole_Papillon_Damour.Domain.UserAggregate.ValueObjects;
using Vole_Papillon_Damour.Domain.WatchlistAggregate;

namespace Vole_Papillon_Damour.Application.tests.WatchlistFeature;

internal sealed class WatchlistFeatureTestFixture : IAsyncDisposable
{
    public static readonly DateTime Now =
        new(2026, 9, 4, 20, 0, 0, DateTimeKind.Utc);

    private readonly SqliteConnection connection;

    private WatchlistFeatureTestFixture(
        SqliteConnection connection,
        WatchlistFeatureTestDbContext context)
    {
        this.connection = connection;
        Context = context;
    }

    public WatchlistFeatureTestDbContext Context { get; }

    public static async Task<WatchlistFeatureTestFixture> CreateAsync(
        int watchlistMaxItems = 100)
    {
        var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<WatchlistFeatureTestDbContext>()
            .UseSqlite(connection)
            .Options;
        var context = new WatchlistFeatureTestDbContext(options);
        await context.Database.EnsureCreatedAsync();
        context.AssociationSettings.Add(AssociationSettings.Create(
            UserId.Create(Guid.Parse("00000000-0000-0000-0000-000000000001")),
            Now));
        await context.SaveChangesAsync();
        context.AssociationSettings.Single().Update(
            duplicateThreshold: 5,
            demandSalesThreshold: 1,
            deadStockMinAgeDays: 30,
            deadStockMinQuantity: 1,
            watchlistMaxItems: watchlistMaxItems,
            alertCooldownDays: 30,
            sessionIdleTimeoutMinutes: 120,
            alertDelayMinutes: 120,
            updatedBy: UserId.Create(Guid.Parse("00000000-0000-0000-0000-000000000001")),
            updatedAt: Now);
        await context.SaveChangesAsync();
        return new WatchlistFeatureTestFixture(connection, context);
    }

    public async Task<Book> AddBookAsync(
        string isbn13,
        string workId,
        string title = "Le livre de test",
        int quantityAvailable = 2)
    {
        Isbn13.TryCreate(isbn13, out var isbn).Should().BeTrue();
        var book = Book.Create(isbn, Now.AddDays(-10));
        book.ApplyAutomaticMetadata(
            new BookMetadataPatch(
                title,
                "Un auteur",
                "Un éditeur",
                2020,
                "Poche",
                "fr",
                "Roman",
                null,
                [
                    BookMetadataField.Title,
                    BookMetadataField.Authors,
                    BookMetadataField.Publisher,
                    BookMetadataField.PublicationYear,
                    BookMetadataField.PhysicalFormat,
                    BookMetadataField.Language,
                    BookMetadataField.Genre,
                    BookMetadataField.WorkId
                ],
                workId),
            BookMetadataSource.Bnf,
            Now.AddDays(-9),
            "{}" );
        for (var index = 0; index < quantityAvailable; index++)
        {
            book.RecordAvailableEntry(Now.AddDays(-8));
        }

        Context.Books.Add(book);
        await Context.SaveChangesAsync();
        return book;
    }

    public async ValueTask DisposeAsync()
    {
        await Context.DisposeAsync();
        await connection.DisposeAsync();
    }
}

internal sealed class WatchlistFeatureTestDbContext(
    DbContextOptions<WatchlistFeatureTestDbContext> options)
    : DbContext(options), IProjectDbContext
{
    public DbSet<User> Users => Set<User>();
    public DbSet<Watchlist> Watchlists => Set<Watchlist>();
    public DbSet<WatchlistItem> WatchlistItems => Set<WatchlistItem>();
    public DbSet<UserAlertHistory> UserAlertHistories => Set<UserAlertHistory>();
    public DbSet<Book> Books => Set<Book>();
    public DbSet<BookAnnouncement> BookAnnouncements => Set<BookAnnouncement>();
    public DbSet<AssoEvents> AssoEvents => Set<AssoEvents>();
    public DbSet<AssociationSettings> AssociationSettings => Set<AssociationSettings>();

    DbSet<Product> IProjectDbContext.Products => throw new NotSupportedException();
    DbSet<Order> IProjectDbContext.Orders => throw new NotSupportedException();
    DbSet<BookMovement> IProjectDbContext.BookMovements => throw new NotSupportedException();
    DbSet<ScanSession> IProjectDbContext.ScanSessions => throw new NotSupportedException();
    DbSet<EmailBounceEvent> IProjectDbContext.EmailBounceEvents => throw new NotSupportedException();
    DbSet<AssoEvents> IProjectDbContext.AssoEvents => AssoEvents;
    DbSet<Book> IProjectDbContext.Books => Books;
    DbSet<BookAnnouncement> IProjectDbContext.BookAnnouncements => BookAnnouncements;
    DbSet<AssociationSettings> IProjectDbContext.AssociationSettings => AssociationSettings;
    DbSet<User> IProjectDbContext.Users => Users;
    DbSet<Watchlist> IProjectDbContext.Watchlists => Watchlists;
    DbSet<WatchlistItem> IProjectDbContext.WatchlistItems => WatchlistItems;
    DbSet<UserAlertHistory> IProjectDbContext.UserAlertHistories => UserAlertHistories;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Ignore<Product>();
        modelBuilder.Ignore<Order>();
        modelBuilder.Ignore<BookMovement>();
        modelBuilder.Ignore<ScanSession>();
        modelBuilder.Ignore<EmailBounceEvent>();

        modelBuilder.Entity<User>(builder =>
        {
            builder.HasKey(user => user.Id);
            builder.Property(user => user.Id)
                .ValueGeneratedNever()
                .HasConversion(id => id.Value, value => UserId.Create(value));
            builder.Property(user => user.ExternalId).HasMaxLength(64);
            builder.Property(user => user.Email).HasMaxLength(320);
            builder.Property(user => user.CreatedAt).HasColumnType("datetime2");
            builder.Property(user => user.LastSeenAt).HasColumnType("datetime2");
            builder.Property(user => user.AnonymizedAt).HasColumnType("datetime2");
            builder.Ignore(user => user.Name);
            builder.Ignore(user => user.Password);
            builder.Ignore(user => user.Salt);
            builder.Ignore(user => user.Role);
        });

        modelBuilder.Entity<Watchlist>(builder =>
        {
            builder.HasKey(watchlist => watchlist.Id);
            builder.Property(watchlist => watchlist.Id)
                .HasColumnName("UserId")
                .ValueGeneratedNever()
                .HasConversion(id => id.Value, value => UserId.Create(value));
            builder.Property(watchlist => watchlist.AlertStatus).HasConversion<byte>();
            builder.Property(watchlist => watchlist.CreatedAt).HasColumnType("datetime2");
            builder.Property(watchlist => watchlist.UpdatedAt).HasColumnType("datetime2");
            builder.HasOne<User>()
                .WithOne()
                .HasForeignKey<Watchlist>(watchlist => watchlist.Id)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<WatchlistItem>(builder =>
        {
            builder.HasKey(item => item.Id);
            builder.Property(item => item.Id).ValueGeneratedNever();
            builder.Property(item => item.UserId)
                .HasConversion(id => id.Value, value => UserId.Create(value));
            builder.Property(item => item.Scope).HasConversion<byte>();
            builder.Property(item => item.WorkId).HasMaxLength(64);
            builder.Property(item => item.Isbn13)
                .HasConversion(new ValueConverter<Isbn13?, string?>(
                    isbn => isbn == null ? null : isbn.Value.Value,
                    value => value == null ? null : ParseIsbn(value)));
            builder.Property(item => item.AddedAt).HasColumnType("datetime2");
            builder.HasOne<Watchlist>()
                .WithMany()
                .HasForeignKey(item => item.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<UserAlertHistory>(builder =>
        {
            builder.HasKey(history => history.Id);
            builder.Property(history => history.Id).ValueGeneratedNever();
            builder.Property(history => history.UserId)
                .HasConversion(id => id.Value, value => UserId.Create(value));
            builder.Property(history => history.Isbn13)
                .HasConversion(isbn => isbn.Value, value => ParseIsbn(value));
            builder.Property(history => history.SentAt).HasColumnType("datetime2");
        });

        modelBuilder.Entity<AssociationSettings>(builder =>
        {
            builder.HasKey(settings => settings.Id);
            builder.Property(settings => settings.UpdatedBy)
                .HasConversion(id => id.Value, value => UserId.Create(value));
        });

        modelBuilder.Entity<Book>(builder =>
        {
            builder.HasKey(book => book.Id);
            builder.Ignore(book => book.Isbn13);
            builder.Property(book => book.Id)
                .ValueGeneratedNever()
                .HasConversion(isbn => isbn.Value, value => ParseIsbn(value));
            builder.Property(book => book.RedirectedToIsbn13)
                .HasConversion(new ValueConverter<Isbn13?, string?>(
                    isbn => isbn == null ? null : isbn.Value.Value,
                    value => value == null ? null : ParseIsbn(value)));
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
                .HasConversion(new ValueConverter<AssoEventsId?, Guid?>(
                    id => id == null ? null : id.Value,
                    value => value.HasValue ? AssoEventsId.Create(value.Value) : null));
            builder.Property(announcement => announcement.ScanSessionId)
                .HasConversion(id => id.Value, value => ScanSessionId.Create(value));
            builder.Property(announcement => announcement.Status).HasConversion<byte>();
        });

        modelBuilder.Entity<AssoEvents>(builder =>
        {
            builder.HasKey(assoEvent => assoEvent.Id);
            builder.Property(assoEvent => assoEvent.Id)
                .ValueGeneratedNever()
                .HasConversion(id => id.Value, value => AssoEventsId.Create(value));
            builder.Property(assoEvent => assoEvent.EventsType)
                .HasConversion(
                    eventsType => (int)eventsType.Value,
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
    }

    private static Isbn13 ParseIsbn(string? value)
    {
        return Isbn13.TryCreate(value, out var isbn)
            ? isbn
            : throw new InvalidOperationException($"Invalid test ISBN: {value}");
    }
}
