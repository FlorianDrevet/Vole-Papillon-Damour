using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using NSubstitute;
using Vole_Papillon_Damour.Application.Books.Queries.GetCatalogDelta;
using Vole_Papillon_Damour.Application.Common.Interfaces.Persistence;
using Vole_Papillon_Damour.Application.Common.Interfaces.Services;
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
using Vole_Papillon_Damour.Domain.RareBookAggregate;
using Vole_Papillon_Damour.Domain.RareBookAggregate.Entities;
using Vole_Papillon_Damour.Domain.RareBookAggregate.ValueObjects;
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
            new GetCatalogDeltaQuery(GeneratedAt.AddMinutes(-5).ToString("O")),
            CancellationToken.None);

        result.IsError.Should().BeFalse();
        result.Value.Books.Should().ContainSingle(book => book.Isbn13 == "9783140464079");
        result.Value.Books.Should().NotContain(book => book.Isbn13 == "9782070363735");
        result.Value.Settings.DuplicateThreshold.Should().Be(7);
        result.Value.NextWatermark.Should().StartWith("v2.");
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
    public async Task Handle_WhenPublishedRareBookExists_ReturnsItsDetailsAndAvailability()
    {
        await using var fixture = await CatalogDeltaFixture.CreateAsync();
        var book = await fixture.AddBookAsync("9782070363735", GeneratedAt.AddMinutes(-1));
        await fixture.AddRareBookAsync(book);

        var result = await fixture.CreateHandler().Handle(
            new GetCatalogDeltaQuery(null),
            CancellationToken.None);

        result.IsError.Should().BeFalse();
        result.Value.Books.Single().IsRare.Should().BeTrue();
        result.Value.RareBooks.Should().ContainSingle();
        result.Value.RareBooks.Single().Price.Should().Be(42.50m);
        result.Value.RareBooks.Single().IsAvailable.Should().BeTrue();
        result.Value.RareBooks.Single().Thumbnail.Should().Be(
            new Uri("https://cdn.example.test/rare-books/cover.jpg"));
    }

    [Fact]
    public async Task Handle_WhenPublishedRareBookHasNoIsbn_ReturnsItForOfflineCashier()
    {
        await using var fixture = await CatalogDeltaFixture.CreateAsync();
        await fixture.AddRareBookWithoutIsbnAsync();

        var result = await fixture.CreateHandler().Handle(
            new GetCatalogDeltaQuery(null),
            CancellationToken.None);

        result.IsError.Should().BeFalse();
        result.Value.RareBooks.Should().ContainSingle(rareBook =>
            rareBook.Title == "Livre rare sans ISBN" &&
            rareBook.Isbn13 == null);
    }

    [Fact]
    public async Task Handle_WhenRareBookWasDeleted_ReturnsItsTombstoneForOfflineRemoval()
    {
        await using var fixture = await CatalogDeltaFixture.CreateAsync();
        var deletedId = RareBookId.Create(Guid.Parse("00000000-0000-0000-0000-000000000099"));
        fixture.Context.RareBookTombstones.Add(
            RareBookTombstone.Create(deletedId, GeneratedAt.AddMinutes(-1)));
        await fixture.SaveAsync();

        var result = await fixture.CreateHandler().Handle(
            new GetCatalogDeltaQuery(GeneratedAt.AddMinutes(-5).ToString("O")),
            CancellationToken.None);

        result.IsError.Should().BeFalse();
        result.Value.RemovedRareBookIds.Should().Contain(deletedId.Value);
    }

    [Fact]
    public async Task Handle_WhenRareBookIsUnpublishedAfterWatermark_ReturnsItAsRemoval()
    {
        await using var fixture = await CatalogDeltaFixture.CreateAsync();
        var rareBook = await fixture.AddRareBookWithoutIsbnAsync();
        rareBook.Unpublish(
            GetCatalogDeltaQueryHandlerTests.GeneratedAt,
            UserId.Create(Guid.Parse("00000000-0000-0000-0000-000000000001")));
        await fixture.SaveAsync();

        var result = await fixture.CreateHandler().Handle(
            new GetCatalogDeltaQuery(GeneratedAt.AddMinutes(-5).ToString("O")),
            CancellationToken.None);

        result.IsError.Should().BeFalse();
        result.Value.RareBooks.Should().BeEmpty();
        result.Value.RemovedRareBookIds.Should().Contain(rareBook.Id.Value);
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
            new GetCatalogDeltaQuery(GeneratedAt.AddMinutes(-5).ToString("O")),
            CancellationToken.None);

        result.IsError.Should().BeFalse();
        result.Value.Books.Single().IsHidden.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_WhenOfflineSaleHasAnOlderBusinessTimestamp_ReturnsTheBookAfterItsRowVersionChanges()
    {
        await using var fixture = await CatalogDeltaFixture.CreateAsync();
        var book = await fixture.AddBookAsync(
            "9782070363735",
            GeneratedAt.AddMinutes(-10));
        fixture.SetRowVersion(book, [0, 0, 0, 0, 0, 0, 0, 1]);
        await fixture.SaveAsync();

        var handler = fixture.CreateHandler();
        var initial = await handler.Handle(
            new GetCatalogDeltaQuery(null),
            CancellationToken.None);
        initial.IsError.Should().BeFalse();

        book.RecordSale(GeneratedAt.AddMinutes(-20));
        fixture.SetRowVersion(book, [0, 0, 0, 0, 0, 0, 0, 2]);
        await fixture.SaveAsync();

        var result = await handler.Handle(
            new GetCatalogDeltaQuery(initial.Value.NextWatermark),
            CancellationToken.None);

        result.IsError.Should().BeFalse();
        result.Value.Books.Should().ContainSingle(book => book.Isbn13 == "9782070363735");
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
            new GetCatalogDeltaQuery(GeneratedAt.AddMinutes(-5).ToString("O")),
            CancellationToken.None);

        result.IsError.Should().BeFalse();
        result.Value.Books.Should().HaveCount(2);
        result.Value.Books.Single(book => book.Isbn13 == "9782070363735").IsWanted.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_AlwaysIncludesTheNextBookFairDatesForOfflineScanMode()
    {
        await using var fixture = await CatalogDeltaFixture.CreateAsync();
        var nextFair = fixture.AddFair(
            "Bourse de septembre",
            new DateTimeOffset(2026, 9, 14, 9, 0, 0, TimeSpan.FromHours(2)),
            new DateTimeOffset(2026, 9, 14, 18, 0, 0, TimeSpan.FromHours(2)));
        fixture.AddFair(
            "Bingo",
            new DateTimeOffset(2026, 9, 10, 9, 0, 0, TimeSpan.FromHours(2)),
            new DateTimeOffset(2026, 9, 10, 18, 0, 0, TimeSpan.FromHours(2)),
            EventsType.EventsTypeEnum.Bingo);
        await fixture.SaveAsync();

        var result = await fixture.CreateHandler().Handle(
            new GetCatalogDeltaQuery(null),
            CancellationToken.None);

        result.IsError.Should().BeFalse();
        result.Value.NextFair.Should().NotBeNull();
        result.Value.NextFair!.Id.Should().Be(nextFair.Id.Value);
        result.Value.NextFair.Name.Should().Be("Bourse de septembre");
        result.Value.NextFair.OpenAt.Should().Be(new DateTimeOffset(2026, 9, 14, 9, 0, 0, TimeSpan.Zero));
        result.Value.NextFair.CloseAt.Should().Be(new DateTimeOffset(2026, 9, 14, 18, 0, 0, TimeSpan.Zero));
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

    public async Task<RareBook> AddRareBookAsync(Book book)
    {
        var volunteerId = UserId.Create(Guid.Parse("00000000-0000-0000-0000-000000000001"));
        var rareBook = RareBook.Create(
            book.Title ?? "Livre rare",
            42.50m,
            GetCatalogDeltaQueryHandlerTests.GeneratedAt.AddMinutes(-1),
            volunteerId,
            authorMention: book.Authors,
            shelf: RareBookShelf.AncientEditions,
            condition: RareBookCondition.AsNew,
            isbn13: book.Id);
        rareBook.Publish(volunteerId, GetCatalogDeltaQueryHandlerTests.GeneratedAt.AddMinutes(-1));
        rareBook.AddPhoto(
            RareBookPhoto.Create(
                rareBook.Id,
                new Uri("https://cdn.example.test/rare-books/cover.jpg"),
                "cover.jpg",
                "Première de couverture",
                0,
                "image/jpeg",
                1024,
                GetCatalogDeltaQueryHandlerTests.GeneratedAt.AddMinutes(-1),
                volunteerId),
            GetCatalogDeltaQueryHandlerTests.GeneratedAt.AddMinutes(-1),
            volunteerId);
        Context.RareBooks.Add(rareBook);
        await Context.SaveChangesAsync();
        return rareBook;
    }

    public async Task<RareBook> AddRareBookWithoutIsbnAsync()
    {
        var volunteerId = UserId.Create(Guid.Parse("00000000-0000-0000-0000-000000000001"));
        var rareBook = RareBook.Create(
            "Livre rare sans ISBN",
            18.00m,
            GetCatalogDeltaQueryHandlerTests.GeneratedAt.AddMinutes(-1),
            volunteerId,
            shelf: RareBookShelf.AncientEditions,
            condition: RareBookCondition.GoodWithFlaws);
        rareBook.Publish(volunteerId, GetCatalogDeltaQueryHandlerTests.GeneratedAt.AddMinutes(-1));
        Context.RareBooks.Add(rareBook);
        await Context.SaveChangesAsync();
        return rareBook;
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

    public AssoEvents AddFair(
        string name,
        DateTimeOffset start,
        DateTimeOffset end,
        EventsType.EventsTypeEnum eventType = EventsType.EventsTypeEnum.Books)
    {
        var fair = AssoEvents.Create(
            name,
            urlImage: null,
            new EventsType(eventType),
            start,
            end,
            new DateTimeOffset(start.Date, TimeSpan.Zero).AddHours(9),
            new DateTimeOffset(end.Date, TimeSpan.Zero).AddHours(18),
            urlImageMap: null,
            new Adresse(12, "Paris", "Rue des livres", 75001),
            urlRegistration: null,
            parties: [],
            description: "Bourse aux livres");
        Context.AssoEvents.Add(fair);
        return fair;
    }

    public async Task SaveAsync() => await Context.SaveChangesAsync();

    public void SetRowVersion(Book book, byte[] rowVersion)
    {
        Context.Entry(book).Property(nameof(Book.RowVersion)).CurrentValue = rowVersion;
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
    public DbSet<AssoEvents> AssoEvents => Set<AssoEvents>();
    public DbSet<RareBook> RareBooks => Set<RareBook>();
    public DbSet<RareBookPhoto> RareBookPhotos => Set<RareBookPhoto>();
    public DbSet<RareBookTombstone> RareBookTombstones => Set<RareBookTombstone>();

    DbSet<Product> IProjectDbContext.Products => throw new NotSupportedException();
    DbSet<User> IProjectDbContext.Users => throw new NotSupportedException();
    DbSet<AssoEvents> IProjectDbContext.AssoEvents => AssoEvents;
    DbSet<Order> IProjectDbContext.Orders => throw new NotSupportedException();
    DbSet<BookMovement> IProjectDbContext.BookMovements => throw new NotSupportedException();
    DbSet<ScanSession> IProjectDbContext.ScanSessions => throw new NotSupportedException();
    DbSet<UserAlertHistory> IProjectDbContext.UserAlertHistories => throw new NotSupportedException();
    DbSet<EmailBounceEvent> IProjectDbContext.EmailBounceEvents => throw new NotSupportedException();
    DbSet<RareBook> IProjectDbContext.RareBooks => RareBooks;
    DbSet<RareBookPhoto> IProjectDbContext.RareBookPhotos => RareBookPhotos;
    DbSet<RareBookTombstone> IProjectDbContext.RareBookTombstones => RareBookTombstones;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        Vole_Papillon_Damour.Application.Common.Persistence.RowVersionQueries.Register(modelBuilder);
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
            builder.Property(item => item.RareBookId)
                .HasConversion(new ValueConverter<RareBookId?, Guid?>(
                    id => id == null ? null : id.Value,
                    value => value == null ? null : RareBookId.Create(value.Value)));
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
        modelBuilder.Ignore<Order>();
        modelBuilder.Ignore<BookMovement>();
        modelBuilder.Ignore<ScanSession>();
        modelBuilder.Ignore<UserAlertHistory>();
        modelBuilder.Ignore<EmailBounceEvent>();

        modelBuilder.Entity<RareBook>(builder =>
        {
            builder.HasKey(book => book.Id);
            builder.Property(book => book.Id)
                .ValueGeneratedNever()
                .HasConversion(id => id.Value, value => RareBookId.Create(value));
            builder.Property(book => book.Slug)
                .HasConversion(slug => slug.Value, value => RareBookSlug.Create(value));
            builder.Property(book => book.Isbn13)
                .HasConversion(new ValueConverter<Isbn13?, string?>(
                    isbn => isbn == null ? null : isbn.Value.Value,
                    value => value == null ? null : ParseIsbn(value)));
            builder.Property(book => book.Shelf)
                .HasConversion(shelf => shelf.Value, value => RareBookShelf.Create(value));
            builder.Property(book => book.Condition)
                .HasConversion(
                    condition => (byte)condition.Value,
                    value => new RareBookCondition((RareBookCondition.RareBookConditionEnum)value));
            builder.Property(book => book.Status).HasConversion<byte>();
            builder.Property(book => book.SoldAtFairId)
                .HasConversion(new ValueConverter<AssoEventsId?, Guid?>(
                    id => id == null ? null : id.Value,
                    value => value.HasValue ? AssoEventsId.Create(value.Value) : null));
            builder.Property(book => book.SoldInSessionId)
                .HasConversion(new ValueConverter<ScanSessionId?, Guid?>(
                    id => id == null ? null : id.Value,
                    value => value.HasValue ? ScanSessionId.Create(value.Value) : null));
            builder.Property(book => book.CreatedBy)
                .HasConversion(id => id.Value, value => UserId.Create(value));
            builder.Property(book => book.UpdatedBy)
                .HasConversion(id => id.Value, value => UserId.Create(value));
            builder.Property(book => book.RowVersion).IsConcurrencyToken();
            builder.HasMany(book => book.Photos)
                .WithOne()
                .HasForeignKey(photo => photo.RareBookId)
                .OnDelete(DeleteBehavior.Cascade);
            builder.Metadata.FindNavigation(nameof(RareBook.Photos))!
                .SetPropertyAccessMode(PropertyAccessMode.Field);
        });

        modelBuilder.Entity<RareBookPhoto>(builder =>
        {
            builder.HasKey(photo => photo.Id);
            builder.Property(photo => photo.Id)
                .ValueGeneratedNever()
                .HasConversion(id => id.Value, value => RareBookPhotoId.Create(value));
            builder.Property(photo => photo.RareBookId)
                .HasConversion(id => id.Value, value => RareBookId.Create(value));
            builder.Property(photo => photo.BlobUri)
                .HasConversion(uri => uri.ToString(), value => new Uri(value, UriKind.Absolute));
            builder.Property(photo => photo.UploadedBy)
                .HasConversion(id => id.Value, value => UserId.Create(value));
        });

        modelBuilder.Entity<RareBookTombstone>(builder =>
        {
            builder.HasKey(tombstone => tombstone.RareBookId);
            builder.Property(tombstone => tombstone.DeletedAt);
        });
    }

    private static Isbn13 ParseIsbn(string value)
    {
        Isbn13.TryCreate(value, out var isbn).Should().BeTrue();
        return isbn;
    }
}
