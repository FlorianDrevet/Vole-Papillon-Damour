using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using Vole_Papillon_Damour.Application.Books.Common;
using Vole_Papillon_Damour.Application.Books.Queries.GetPublicBook;
using Vole_Papillon_Damour.Application.Books.Queries.GetPublicNextBookFair;
using Vole_Papillon_Damour.Application.Books.Queries.GetPublicWork;
using Vole_Papillon_Damour.Application.Books.Queries.GetPublicCatalogSitemap;
using Vole_Papillon_Damour.Application.Books.Queries.SearchCatalog;
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
using Vole_Papillon_Damour.Domain.ScanSessionAggregate;
using Vole_Papillon_Damour.Domain.ScanSessionAggregate.ValueObjects;
using Vole_Papillon_Damour.Domain.UserAggregate;
using Vole_Papillon_Damour.Domain.UserAggregate.ValueObjects;
using Vole_Papillon_Damour.Domain.WatchlistAggregate;

namespace Vole_Papillon_Damour.Application.tests.Books.Queries;

public sealed class PublicCatalogQueryHandlerTests
{
    internal static readonly DateTime Now =
        new(2026, 9, 4, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task SearchCatalog_WhenQueryMatchesTitle_ReturnsVisibleBookWithSeparateAvailability()
    {
        await using var fixture = await PublicCatalogFixture.CreateAsync();
        var book = fixture.AddBook(
            "9782070408504",
            "Le Petit Prince",
            "Antoine de Saint-Exupéry",
            genre: "Romans",
            quantityAvailable: 3);
        fixture.AddAnnouncement(book, quantity: 2);
        fixture.AddBook("9782070363735", "Titre masqué", "Auteur", hidden: true);
        await fixture.SaveAsync();

        var result = await fixture.CreateSearchHandler().Handle(
            new SearchCatalogQuery("petit", null, PublicCatalogAvailabilityFilter.All,
                RareOnly: false, PublicCatalogSortOrder.Relevance, Page: 1, PageSize: 20),
            CancellationToken.None);

        result.IsError.Should().BeFalse();
        result.Value.TotalCount.Should().Be(1);
        var found = result.Value.Books.Should().ContainSingle().Which;
        found.Isbn13.Should().Be("9782070408504");
        found.QuantityAvailable.Should().Be(3);
        found.QuantityAnnounced.Should().Be(2);
        found.NextFairAt.Should().BeNull();
    }

    [Fact]
    public async Task SearchCatalog_WhenQueryHasNoAccents_FindsAccentedTitleAndAppliesRareGenreFilter()
    {
        await using var fixture = await PublicCatalogFixture.CreateAsync();
        fixture.AddBook(
            "9782253006329",
            "L'Écume des jours",
            "Boris Vian",
            genre: "Romans",
            rare: true,
            quantityAvailable: 1);
        fixture.AddBook(
            "9782070363735",
            "L'Écume des jours — autre édition",
            "Boris Vian",
            genre: "Romans",
            quantityAvailable: 1);
        await fixture.SaveAsync();

        var result = await fixture.CreateSearchHandler().Handle(
            new SearchCatalogQuery("ecume", "Romans", PublicCatalogAvailabilityFilter.AvailableNow,
                RareOnly: true, PublicCatalogSortOrder.Relevance, Page: 1, PageSize: 20),
            CancellationToken.None);

        result.IsError.Should().BeFalse();
        result.Value.Books.Should().ContainSingle(book => book.Isbn13 == "9782253006329");
    }

    [Fact]
    public async Task SearchCatalog_WhenAnnouncementBelongsToCancelledFair_DoesNotExposeIt()
    {
        await using var fixture = await PublicCatalogFixture.CreateAsync();
        var book = fixture.AddBook("9782070408504", "Le Petit Prince", "Antoine de Saint-Exupéry");
        var cancelledFair = fixture.AddFair(
            "Bourse annulée",
            fixture.NowOffset.AddDays(3),
            fixture.NowOffset.AddDays(4));
        cancelledFair.Cancel();
        fixture.AddAnnouncement(book, quantity: 2, assoEventsId: cancelledFair.Id);
        await fixture.SaveAsync();

        var result = await fixture.CreateSearchHandler().Handle(
            new SearchCatalogQuery("petit", null, PublicCatalogAvailabilityFilter.All,
                RareOnly: false, PublicCatalogSortOrder.Relevance, Page: 1, PageSize: 20),
            CancellationToken.None);

        result.IsError.Should().BeFalse();
        result.Value.Books.Should().ContainSingle().Which.QuantityAnnounced.Should().Be(0);
    }

    [Fact]
    public async Task SearchCatalog_WhenAnnouncementBelongsToNonBookEvent_DoesNotExposeIt()
    {
        await using var fixture = await PublicCatalogFixture.CreateAsync();
        var book = fixture.AddBook("9782070408504", "Le Petit Prince", "Antoine de Saint-Exupéry");
        var otherEvent = fixture.AddFair(
            "Événement associatif",
            fixture.NowOffset.AddDays(3),
            fixture.NowOffset.AddDays(4),
            EventsType.EventsTypeEnum.Other);
        fixture.AddAnnouncement(book, quantity: 2, assoEventsId: otherEvent.Id);
        await fixture.SaveAsync();

        var result = await fixture.CreateSearchHandler().Handle(
            new SearchCatalogQuery("petit", null, PublicCatalogAvailabilityFilter.All,
                RareOnly: false, PublicCatalogSortOrder.Relevance, Page: 1, PageSize: 20),
            CancellationToken.None);

        result.IsError.Should().BeFalse();
        result.Value.Books.Should().ContainSingle().Which.QuantityAnnounced.Should().Be(0);
    }

    [Fact]
    public async Task SearchCatalog_WhenAvailabilityIsAll_KeepsExhaustedBooksVisible()
    {
        await using var fixture = await PublicCatalogFixture.CreateAsync();
        fixture.AddBook("9782070408504", "Le Petit Prince", "Antoine de Saint-Exupéry");
        await fixture.SaveAsync();

        var result = await fixture.CreateSearchHandler().Handle(
            new SearchCatalogQuery(null, null, PublicCatalogAvailabilityFilter.All,
                RareOnly: false, PublicCatalogSortOrder.RecentlyAdded, Page: 1, PageSize: 20),
            CancellationToken.None);

        result.IsError.Should().BeFalse();
        result.Value.Books.Should().ContainSingle();
        result.Value.Books[0].QuantityAvailable.Should().Be(0);
    }

    [Fact]
    public async Task GetPublicBook_WhenBookIsVisible_ReturnsItsCanonicalPublicProjection()
    {
        await using var fixture = await PublicCatalogFixture.CreateAsync();
        fixture.AddBook(
            "9782070408504",
            "Le Petit Prince",
            "Antoine de Saint-Exupéry",
            publisher: "Gallimard",
            publicationYear: 1999,
            genre: "Romans",
            workId: "work-antoine-1",
            coverUrl: "https://covers.example/petit-prince.jpg",
            quantityAvailable: 2);
        await fixture.SaveAsync();

        var result = await fixture.CreatePublicBookHandler().Handle(
            new GetPublicBookQuery("9782070408504"),
            CancellationToken.None);

        result.IsError.Should().BeFalse();
        result.Value.Isbn13.Should().Be("9782070408504");
        result.Value.Title.Should().Be("Le Petit Prince");
        result.Value.Publisher.Should().Be("Gallimard");
        result.Value.PublicationYear.Should().Be(1999);
        result.Value.WorkId.Should().Be("work-antoine-1");
        result.Value.CoverUrl.Should().Be("https://covers.example/petit-prince.jpg");
        result.Value.QuantityAvailable.Should().Be(2);
    }

    [Fact]
    public async Task GetPublicBook_WhenBookIsHidden_ReturnsNotFound()
    {
        await using var fixture = await PublicCatalogFixture.CreateAsync();
        fixture.AddBook("9782070408504", "Le Petit Prince", "Antoine de Saint-Exupéry", hidden: true);
        await fixture.SaveAsync();

        var result = await fixture.CreatePublicBookHandler().Handle(
            new GetPublicBookQuery("9782070408504"),
            CancellationToken.None);

        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("Book.NotFound");
    }

    [Fact]
    public async Task GetNextBookFair_SkipsCancelledFairsAndReturnsPublicSchedule()
    {
        await using var fixture = await PublicCatalogFixture.CreateAsync();
        var cancelledFair = fixture.AddFair(
            "Bourse annulée",
            fixture.NowOffset.AddDays(3),
            fixture.NowOffset.AddDays(4));
        cancelledFair.Cancel();
        fixture.AddFair(
            "Bourse de printemps",
            fixture.NowOffset.AddDays(10),
            fixture.NowOffset.AddDays(11));
        await fixture.SaveAsync();

        var result = await fixture.CreateNextFairHandler().Handle(
            new GetPublicNextBookFairQuery(),
            CancellationToken.None);

        result.IsError.Should().BeFalse();
        result.Value.Name.Should().Be("Bourse de printemps");
        result.Value.DateStart.Should().Be(fixture.NowOffset.AddDays(10));
        result.Value.OpenAt.Should().Be(fixture.NowOffset.AddDays(10).AddHours(9));
        result.Value.CloseAt.Should().Be(fixture.NowOffset.AddDays(10).AddHours(18));
        result.Value.City.Should().Be("Paris");
    }

    [Fact]
    public async Task GetPublicWork_ReturnsVisibleEditionsOfTheSameWork()
    {
        await using var fixture = await PublicCatalogFixture.CreateAsync();
        fixture.AddBook(
            "9782070408504",
            "Le Petit Prince",
            "Antoine de Saint-Exupéry",
            workId: "work-antoine-1",
            quantityAvailable: 1);
        fixture.AddBook(
            "9782070363735",
            "Le Petit Prince — édition poche",
            "Antoine de Saint-Exupéry",
            workId: "work-antoine-1",
            quantityAvailable: 0);
        fixture.AddBook(
            "9782253006329",
            "Un autre livre",
            "Une autrice",
            workId: "other-work");
        await fixture.SaveAsync();

        var result = await fixture.CreatePublicWorkHandler().Handle(
            new GetPublicWorkQuery("work-antoine-1"),
            CancellationToken.None);

        result.IsError.Should().BeFalse();
        result.Value.WorkId.Should().Be("work-antoine-1");
        result.Value.Title.Should().Be("Le Petit Prince");
        result.Value.Editions.Should().HaveCount(2);
        result.Value.Editions.Should().OnlyContain(book => book.WorkId == "work-antoine-1");
    }

    [Fact]
    public async Task GetPublicWork_WhenAllEditionsAreRedirected_ReturnsNotFound()
    {
        await using var fixture = await PublicCatalogFixture.CreateAsync();
        var canonical = fixture.AddBook(
            "9782070408504",
            "Le Petit Prince",
            "Antoine de Saint-Exupéry",
            workId: "work-antoine-1");
        var redirected = fixture.AddBook(
            "9782253006329",
            "Le Petit Prince — édition absorbée",
            "Antoine de Saint-Exupéry",
            workId: "work-antoine-1");
        Isbn13.TryCreate(canonical.Id.Value, out var canonicalIsbn).Should().BeTrue();
        redirected.RedirectTo(canonicalIsbn);
        canonical.UpdateCatalogVisibility(isHidden: true, PublicCatalogQueryHandlerTests.Now);
        await fixture.SaveAsync();

        var result = await fixture.CreatePublicWorkHandler().Handle(
            new GetPublicWorkQuery("work-antoine-1"),
            CancellationToken.None);

        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("Catalog.WorkNotFound");
    }

    [Fact]
    public async Task GetPublicCatalogSitemap_ContainsCanonicalVisibleBookUrlsOnly()
    {
        await using var fixture = await PublicCatalogFixture.CreateAsync();
        fixture.AddBook(
            "9782070408504",
            "Le Petit Prince",
            "Antoine de Saint-Exupéry");
        var redirected = fixture.AddBook(
            "9782253006329",
            "Une édition absorbée",
            "Auteur");
        Isbn13.TryCreate("9782070408504", out var canonical).Should().BeTrue();
        redirected.RedirectTo(canonical);
        fixture.AddBook(
            "9782070363735",
            "Fiche masquée",
            "Auteur",
            hidden: true);
        await fixture.SaveAsync();

        var result = await fixture.CreateSitemapHandler().Handle(
            new GetPublicCatalogSitemapQuery(),
            CancellationToken.None);

        result.IsError.Should().BeFalse();
        result.Value.Entries.Should().ContainSingle();
        result.Value.Entries[0].UrlPath.Should().Be(
            "/livres/le-petit-prince-antoine-de-saint-exupery-9782070408504");
    }
}

internal sealed class PublicCatalogFixture : IAsyncDisposable
{
    private readonly SqliteConnection connection;
    private readonly DateTime now = PublicCatalogQueryHandlerTests.Now;

    private PublicCatalogFixture(SqliteConnection connection, PublicCatalogTestDbContext context)
    {
        this.connection = connection;
        Context = context;
    }

    public PublicCatalogTestDbContext Context { get; }

    public DateTimeOffset NowOffset => new(now, TimeSpan.Zero);

    public static async Task<PublicCatalogFixture> CreateAsync()
    {
        var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<PublicCatalogTestDbContext>()
            .UseSqlite(connection)
            .Options;
        var context = new PublicCatalogTestDbContext(options);
        await context.Database.EnsureCreatedAsync();
        return new PublicCatalogFixture(connection, context);
    }

    public Book AddBook(
        string isbn,
        string title,
        string authors,
        string? publisher = null,
        int? publicationYear = null,
        string? genre = null,
        string? workId = null,
        string? coverUrl = null,
        int quantityAvailable = 0,
        bool rare = false,
        bool hidden = false)
    {
        Isbn13.TryCreate(isbn, out var isbn13).Should().BeTrue();
        var book = Book.Create(isbn13, now.AddMinutes(-10));
        for (var index = 0; index < quantityAvailable; index++)
        {
            book.RecordAvailableEntry(now.AddMinutes(-index - 1));
        }

        book.ApplyManualMetadata(
            new BookMetadataPatch(
                title,
                authors,
                publisher,
                publicationYear,
                PhysicalFormat: null,
                Language: "fr",
                genre,
                coverUrl,
                [
                    BookMetadataField.Title,
                    BookMetadataField.Authors,
                    BookMetadataField.Publisher,
                    BookMetadataField.PublicationYear,
                    BookMetadataField.Language,
                    BookMetadataField.Genre,
                    BookMetadataField.CoverBlobRef,
                    BookMetadataField.WorkId
                ],
                workId),
            now.AddMinutes(-5));
        book.UpdateRareStatus(rare, now.AddMinutes(-4));
        book.UpdateCatalogVisibility(hidden, now.AddMinutes(-3));
        Context.Books.Add(book);
        return book;
    }

    public void AddAnnouncement(Book book, int quantity, AssoEventsId? assoEventsId = null)
    {
        Context.BookAnnouncements.Add(
            BookAnnouncement.Create(
                BookAnnouncementId.CreateUnique(),
                book.Id,
                assoEventsId,
                quantity,
                now.AddMinutes(-2),
                ScanSessionId.Create(Guid.Parse("00000000-0000-0000-0000-000000000001"))));
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
            start.AddHours(9),
            start.AddHours(18),
            urlImageMap: null,
            new Adresse(12, "Paris", "Rue des livres", 75001),
            urlRegistration: null,
            parties: [],
            description: "Bourse aux livres");
        Context.AssoEvents.Add(fair);
        return fair;
    }

    public async Task SaveAsync() => await Context.SaveChangesAsync();

    public SearchCatalogQueryHandler CreateSearchHandler()
    {
        var clock = Substitute.For<IDateTimeProvider>();
        clock.UtcNow.Returns(now);
        return new SearchCatalogQueryHandler(Context, clock);
    }

    public GetPublicBookQueryHandler CreatePublicBookHandler()
    {
        var clock = Substitute.For<IDateTimeProvider>();
        clock.UtcNow.Returns(now);
        return new GetPublicBookQueryHandler(Context, clock);
    }

    public GetPublicNextBookFairQueryHandler CreateNextFairHandler()
    {
        var clock = Substitute.For<IDateTimeProvider>();
        clock.UtcNow.Returns(now);
        return new GetPublicNextBookFairQueryHandler(Context, clock);
    }

    public GetPublicWorkQueryHandler CreatePublicWorkHandler()
    {
        var clock = Substitute.For<IDateTimeProvider>();
        clock.UtcNow.Returns(now);
        return new GetPublicWorkQueryHandler(Context, clock);
    }

    public GetPublicCatalogSitemapQueryHandler CreateSitemapHandler()
    {
        var clock = Substitute.For<IDateTimeProvider>();
        clock.UtcNow.Returns(now);
        return new GetPublicCatalogSitemapQueryHandler(Context, clock);
    }

    public async ValueTask DisposeAsync()
    {
        await Context.DisposeAsync();
        await connection.DisposeAsync();
    }
}

internal sealed class PublicCatalogTestDbContext(DbContextOptions<PublicCatalogTestDbContext> options)
    : DbContext(options), IProjectDbContext
{
    public DbSet<Book> Books => Set<Book>();
    public DbSet<BookAnnouncement> BookAnnouncements => Set<BookAnnouncement>();
    public DbSet<AssoEvents> AssoEvents => Set<AssoEvents>();

    DbSet<Product> IProjectDbContext.Products => throw new NotSupportedException();
    DbSet<User> IProjectDbContext.Users => throw new NotSupportedException();
    DbSet<Order> IProjectDbContext.Orders => throw new NotSupportedException();
    DbSet<BookMovement> IProjectDbContext.BookMovements => throw new NotSupportedException();
    DbSet<ScanSession> IProjectDbContext.ScanSessions => throw new NotSupportedException();
    DbSet<AssociationSettings> IProjectDbContext.AssociationSettings => throw new NotSupportedException();
    DbSet<Watchlist> IProjectDbContext.Watchlists => throw new NotSupportedException();
    DbSet<WatchlistItem> IProjectDbContext.WatchlistItems => throw new NotSupportedException();
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
            builder.Property(announcement => announcement.AssoEventsId)
                .HasConversion(
                    id => id == null ? (Guid?)null : id.Value,
                    value => value.HasValue ? AssoEventsId.Create(value.Value) : null);
            builder.Property(announcement => announcement.Status).HasConversion<byte>();
            builder.Ignore(announcement => announcement.CreatedAt);
            builder.Ignore(announcement => announcement.ReleasedAt);
            builder.Ignore(announcement => announcement.ScanSessionId);
            builder.Ignore(announcement => announcement.ClientGestureId);
        });

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
            builder.ComplexProperty(assoEvent => assoEvent.Adresse);
            builder.Ignore(assoEvent => assoEvent.UrlImage);
            builder.Ignore(assoEvent => assoEvent.UrlRegistration);
            builder.Ignore(assoEvent => assoEvent.UrlImageMap);
            builder.Ignore(assoEvent => assoEvent.Parties);
            builder.Ignore(assoEvent => assoEvent.BingoNumeros);
        });

        modelBuilder.Ignore<Product>();
        modelBuilder.Ignore<User>();
        modelBuilder.Ignore<Order>();
        modelBuilder.Ignore<BookMovement>();
        modelBuilder.Ignore<ScanSession>();
        modelBuilder.Ignore<AssociationSettings>();
        modelBuilder.Ignore<Watchlist>();
        modelBuilder.Ignore<WatchlistItem>();
        modelBuilder.Ignore<UserAlertHistory>();
        modelBuilder.Ignore<EmailBounceEvent>();
    }

    private static Isbn13 ParseIsbn(string value)
    {
        Isbn13.TryCreate(value, out var isbn).Should().BeTrue();
        return isbn;
    }
}
