using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Vole_Papillon_Damour.Application.Books.Queries.GetDeadStock;
using Vole_Papillon_Damour.Application.Common.Interfaces.Persistence;
using Vole_Papillon_Damour.Application.Common.Interfaces.Services;
using Vole_Papillon_Damour.Domain.AssoEventsAggregate;
using Vole_Papillon_Damour.Domain.AssociationSettingsAggregate;
using Vole_Papillon_Damour.Domain.BookAggregate;
using Vole_Papillon_Damour.Domain.BookAggregate.Entities;
using Vole_Papillon_Damour.Domain.BookAggregate.ValueObjects;
using Vole_Papillon_Damour.Domain.BookMovementAggregate;
using Vole_Papillon_Damour.Domain.BookMovementAggregate.ValueObjects;
using Vole_Papillon_Damour.Domain.OrderAggregate;
using Vole_Papillon_Damour.Domain.ProductAggregate;
using Vole_Papillon_Damour.Domain.ScanSessionAggregate;
using Vole_Papillon_Damour.Domain.UserAggregate;
using Vole_Papillon_Damour.Domain.WatchlistAggregate;

namespace Vole_Papillon_Damour.Application.tests.Books.Queries;

public sealed class GetDeadStockQueryHandlerTests
{
    internal static readonly DateTime Now =
        new(2026, 9, 4, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task Handle_WhenBooksMatchDeadStockRules_ReturnsThemByQuantityDescending()
    {
        await using var fixture = await DeadStockFixture.CreateAsync();
        fixture.AddBook(
            "9782070408504",
            "Le titre avec le plus de stock",
            firstAvailableAt: Now.AddMonths(-12),
            quantityAvailable: 8);
        fixture.AddBook(
            "9782070363735",
            "Le titre suivant",
            firstAvailableAt: Now.AddMonths(-7),
            quantityAvailable: 4);
        fixture.AddBook(
            "9782253006329",
            "Trop récent",
            firstAvailableAt: Now.AddMonths(-5),
            quantityAvailable: 20);
        fixture.AddBook(
            "9783140464079",
            "Au seuil exact",
            firstAvailableAt: Now.AddMonths(-8),
            quantityAvailable: 3);
        await fixture.SaveAsync();

        var result = await fixture.CreateHandler().Handle(
            new GetDeadStockQuery(MinAgeMonths: 6, MinQuantity: 3),
            CancellationToken.None);

        result.IsError.Should().BeFalse();
        result.Value.GeneratedAt.Should().Be(Now);
        result.Value.Books.Select(book => book.Isbn13)
            .Should()
            .Equal("9782070408504", "9782070363735");
        result.Value.Books[0].QuantityAvailable.Should().Be(8);
        result.Value.Books[0].FirstAvailableAt.Should().Be(Now.AddMonths(-12));
    }

    [Fact]
    public async Task Handle_WhenBookHasSaleMovement_ExcludesItEvenIfProjectionSalesCountIsZero()
    {
        await using var fixture = await DeadStockFixture.CreateAsync();
        var book = fixture.AddBook(
            "9782070408504",
            "Déjà vendu",
            firstAvailableAt: Now.AddMonths(-12),
            quantityAvailable: 5);
        fixture.AddSaleMovement(book.Id, Now.AddMonths(-3));
        await fixture.SaveAsync();

        var result = await fixture.CreateHandler().Handle(
            new GetDeadStockQuery(6, 3),
            CancellationToken.None);

        result.IsError.Should().BeFalse();
        result.Value.Books.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_WhenBookIsRedirected_ExcludesTheAbsorbedFiche()
    {
        await using var fixture = await DeadStockFixture.CreateAsync();
        var canonical = fixture.AddBook(
            "9782070408504",
            "Fiche canonique",
            firstAvailableAt: Now.AddMonths(-12),
            quantityAvailable: 2);
        var redirected = fixture.AddBook(
            "9782070363735",
            "Fiche absorbée",
            firstAvailableAt: Now.AddMonths(-12),
            quantityAvailable: 8);
        redirected.RedirectTo(canonical.Id);
        await fixture.SaveAsync();

        var result = await fixture.CreateHandler().Handle(
            new GetDeadStockQuery(6, 1),
            CancellationToken.None);

        result.IsError.Should().BeFalse();
        result.Value.Books.Should().ContainSingle(book => book.Isbn13 == canonical.Id.Value);
    }

    [Theory]
    [InlineData(0, 3)]
    [InlineData(-1, 3)]
    [InlineData(6, -1)]
    [InlineData(int.MaxValue, 3)]
    public async Task Handle_WhenFilterIsInvalid_ReturnsValidationError(int minAgeMonths, int minQuantity)
    {
        await using var fixture = await DeadStockFixture.CreateAsync();

        var result = await fixture.CreateHandler().Handle(
            new GetDeadStockQuery(minAgeMonths, minQuantity),
            CancellationToken.None);

        result.IsError.Should().BeTrue();
        result.Errors.Should().OnlyContain(error => error.Type == ErrorOr.ErrorType.Validation);
    }

    [Fact]
    public async Task Handle_WhenClockIsNotUtc_ReturnsValidationError()
    {
        await using var fixture = await DeadStockFixture.CreateAsync();
        fixture.Clock.UtcNow.Returns(new DateTime(2026, 9, 4, 12, 0, 0, DateTimeKind.Local));

        var result = await fixture.CreateHandler().Handle(
            new GetDeadStockQuery(6, 3),
            CancellationToken.None);

        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("Books.InvalidClock");
    }
}

internal sealed class DeadStockFixture : IAsyncDisposable
{
    private readonly SqliteConnection connection;

    private DeadStockFixture(SqliteConnection connection, DeadStockTestDbContext context)
    {
        this.connection = connection;
        Context = context;
        Clock = Substitute.For<IDateTimeProvider>();
        Clock.UtcNow.Returns(GetDeadStockQueryHandlerTests.Now);
        Logger = Substitute.For<ILogger<GetDeadStockQueryHandler>>();
    }

    public DeadStockTestDbContext Context { get; }
    public IDateTimeProvider Clock { get; }
    public ILogger<GetDeadStockQueryHandler> Logger { get; }

    public static async Task<DeadStockFixture> CreateAsync()
    {
        var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<DeadStockTestDbContext>()
            .UseSqlite(connection)
            .Options;
        var context = new DeadStockTestDbContext(options);
        await context.Database.EnsureCreatedAsync();
        return new DeadStockFixture(connection, context);
    }

    public Book AddBook(
        string isbn,
        string title,
        DateTime firstAvailableAt,
        int quantityAvailable)
    {
        var book = Book.Create(ParseIsbn(isbn), firstAvailableAt);
        book.ApplyManualMetadata(
            new BookMetadataPatch(
                Title: title,
                Authors: null,
                Publisher: null,
                PublicationYear: null,
                PhysicalFormat: null,
                Language: null,
                Genre: null,
                CoverUrl: null,
                Fields: [BookMetadataField.Title]),
            firstAvailableAt);
        for (var index = 0; index < quantityAvailable; index++)
        {
            book.RecordAvailableEntry(firstAvailableAt);
        }

        Context.Books.Add(book);
        if (quantityAvailable > 0)
        {
            Context.BookMovements.Add(BookMovement.Create(
                BookMovementId.CreateUnique(),
                book.Id,
                BookMovementType.DirectEntry,
                quantityAvailable,
                firstAvailableAt,
                firstAvailableAt,
                clockSuspect: false,
                scanSessionId: null,
                volunteerId: null,
                assoEventsId: null,
                note: null,
                clientGestureId: null));
        }

        return book;
    }

    public void AddSaleMovement(Isbn13 isbn13, DateTime occurredAt)
    {
        Context.BookMovements.Add(BookMovement.Create(
            BookMovementId.CreateUnique(),
            isbn13,
            BookMovementType.Sale,
            quantity: -1,
            occurredAt,
            occurredAt,
            clockSuspect: false,
            scanSessionId: null,
            volunteerId: null,
            assoEventsId: null,
            note: null,
            clientGestureId: null));
    }

    public async Task SaveAsync() => await Context.SaveChangesAsync();

    public GetDeadStockQueryHandler CreateHandler() => new(Context, Clock, Logger);

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

internal sealed class DeadStockTestDbContext(DbContextOptions<DeadStockTestDbContext> options)
    : DbContext(options), IProjectDbContext
{
    public DbSet<Book> Books => Set<Book>();
    public DbSet<BookMovement> BookMovements => Set<BookMovement>();

    DbSet<Product> IProjectDbContext.Products => throw new NotSupportedException();
    DbSet<User> IProjectDbContext.Users => throw new NotSupportedException();
    DbSet<AssoEvents> IProjectDbContext.AssoEvents => throw new NotSupportedException();
    DbSet<Order> IProjectDbContext.Orders => throw new NotSupportedException();
    DbSet<BookAnnouncement> IProjectDbContext.BookAnnouncements => throw new NotSupportedException();
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
            builder.Ignore(book => book.RowVersion);
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
            builder.Ignore(movement => movement.ScanSessionId);
            builder.Ignore(movement => movement.VolunteerId);
            builder.Ignore(movement => movement.AssoEventsId);
            builder.Ignore(movement => movement.ReversalOfMovementId);
        });

        modelBuilder.Ignore<BookAnnouncement>();
        modelBuilder.Ignore<AssoEvents>();
        modelBuilder.Ignore<Product>();
        modelBuilder.Ignore<User>();
        modelBuilder.Ignore<Order>();
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
