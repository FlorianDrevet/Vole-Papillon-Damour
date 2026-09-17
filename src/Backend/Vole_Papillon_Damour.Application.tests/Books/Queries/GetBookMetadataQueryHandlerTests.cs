using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using Vole_Papillon_Damour.Application.Books.Common;
using Vole_Papillon_Damour.Application.Books.Queries.GetBookMetadata;
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
using Vole_Papillon_Damour.Domain.UserAggregate;
using Vole_Papillon_Damour.Domain.WatchlistAggregate;

namespace Vole_Papillon_Damour.Application.tests.Books.Queries;

public sealed class GetBookMetadataQueryHandlerTests
{
    [Fact]
    public async Task Handle_WhenBookIsUnknown_CallsResolverAndReturnsItsMetadata()
    {
        await using var fixture = await GetBookMetadataFixture.CreateAsync();
        Isbn13.TryCreate("9782070363735", out var isbn13).Should().BeTrue();
        var metadata = new BookMetadataResult(
            isbn13.Value,
            "Le Petit Prince",
            "Antoine de Saint-Exupéry",
            "Gallimard",
            1946,
            new Uri("https://covers.example.test/petit-prince.jpg"),
            "BnF",
            "OL123W",
            new DateTimeOffset(2026, 9, 3, 8, 0, 0, TimeSpan.Zero));
        var resolver = Substitute.For<IBibliographicMetadataResolver>();
        resolver.ResolveAsync(isbn13, Arg.Any<CancellationToken>()).Returns(metadata);
        var handler = new GetBookMetadataQueryHandler(fixture.Context, resolver);

        var result = await handler.Handle(new GetBookMetadataQuery(isbn13), CancellationToken.None);

        result.IsError.Should().BeFalse();
        result.Value.Should().Be(metadata);
    }

    [Fact]
    public async Task Handle_WhenResolverFindsNothing_ReturnsNotFoundError()
    {
        await using var fixture = await GetBookMetadataFixture.CreateAsync();
        Isbn13.TryCreate("9782070363735", out var isbn13).Should().BeTrue();
        var resolver = Substitute.For<IBibliographicMetadataResolver>();
        resolver.ResolveAsync(isbn13, Arg.Any<CancellationToken>()).Returns((BookMetadataResult?)null);
        var handler = new GetBookMetadataQueryHandler(fixture.Context, resolver);

        var result = await handler.Handle(new GetBookMetadataQuery(isbn13), CancellationToken.None);

        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("Book.MetadataNotFound");
    }

    [Fact]
    public async Task Handle_WhenBookIsAlreadyResolved_ReturnsItFromTheDatabaseWithoutCallingTheResolver()
    {
        await using var fixture = await GetBookMetadataFixture.CreateAsync();
        Isbn13.TryCreate("9782070363735", out var isbn13).Should().BeTrue();
        var book = Book.Create(isbn13, new DateTime(2026, 9, 1, 8, 0, 0, DateTimeKind.Utc));
        book.ApplyAutomaticMetadata(
            new BookMetadataPatch(
                "Le Petit Prince",
                "Antoine de Saint-Exupéry",
                "Gallimard",
                1946,
                PhysicalFormat: null,
                Language: null,
                Genre: "Jeunesse",
                CoverUrl: "https://covers.example.test/petit-prince.jpg",
                Fields: [BookMetadataField.Title, BookMetadataField.Authors, BookMetadataField.Publisher,
                    BookMetadataField.PublicationYear, BookMetadataField.Genre, BookMetadataField.CoverUrl],
                WorkId: "OL123W"),
            BookMetadataSource.Bnf,
            new DateTime(2026, 9, 1, 9, 0, 0, DateTimeKind.Utc),
            rawPayload: null,
            coverSource: BookCoverSource.Bnf,
            coverCheckedAt: new DateTime(2026, 9, 1, 9, 0, 0, DateTimeKind.Utc));
        fixture.Context.Books.Add(book);
        await fixture.Context.SaveChangesAsync();
        var resolver = Substitute.For<IBibliographicMetadataResolver>();
        var handler = new GetBookMetadataQueryHandler(fixture.Context, resolver);

        var result = await handler.Handle(new GetBookMetadataQuery(isbn13), CancellationToken.None);

        result.IsError.Should().BeFalse();
        result.Value.Title.Should().Be("Le Petit Prince");
        result.Value.Source.Should().Be("BnF");
        result.Value.CoverSource.Should().Be("BnF");
        await resolver.DidNotReceive().ResolveAsync(Arg.Any<Isbn13>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WhenBookIsAlreadyMarkedNotFound_ReturnsNotFoundWithoutCallingTheResolver()
    {
        await using var fixture = await GetBookMetadataFixture.CreateAsync();
        Isbn13.TryCreate("9782070363735", out var isbn13).Should().BeTrue();
        var book = Book.Create(isbn13, new DateTime(2026, 9, 1, 8, 0, 0, DateTimeKind.Utc));
        book.RecordMetadataNotFound(new DateTime(2026, 9, 1, 9, 0, 0, DateTimeKind.Utc));
        fixture.Context.Books.Add(book);
        await fixture.Context.SaveChangesAsync();
        var resolver = Substitute.For<IBibliographicMetadataResolver>();
        var handler = new GetBookMetadataQueryHandler(fixture.Context, resolver);

        var result = await handler.Handle(new GetBookMetadataQuery(isbn13), CancellationToken.None);

        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("Book.MetadataNotFound");
        await resolver.DidNotReceive().ResolveAsync(Arg.Any<Isbn13>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WhenBookIsStillPending_CallsTheResolver()
    {
        await using var fixture = await GetBookMetadataFixture.CreateAsync();
        Isbn13.TryCreate("9782070363735", out var isbn13).Should().BeTrue();
        fixture.Context.Books.Add(Book.Create(isbn13, new DateTime(2026, 9, 1, 8, 0, 0, DateTimeKind.Utc)));
        await fixture.Context.SaveChangesAsync();
        var metadata = new BookMetadataResult(
            isbn13.Value, "Le Petit Prince", null, null, null, null, "BnF", null,
            new DateTimeOffset(2026, 9, 3, 8, 0, 0, TimeSpan.Zero));
        var resolver = Substitute.For<IBibliographicMetadataResolver>();
        resolver.ResolveAsync(isbn13, Arg.Any<CancellationToken>()).Returns(metadata);
        var handler = new GetBookMetadataQueryHandler(fixture.Context, resolver);

        var result = await handler.Handle(new GetBookMetadataQuery(isbn13), CancellationToken.None);

        result.IsError.Should().BeFalse();
        result.Value.Should().Be(metadata);
    }
}

internal sealed class GetBookMetadataFixture : IAsyncDisposable
{
    private readonly SqliteConnection connection;

    private GetBookMetadataFixture(SqliteConnection connection, GetBookMetadataTestDbContext context)
    {
        this.connection = connection;
        Context = context;
    }

    public GetBookMetadataTestDbContext Context { get; }

    public static async Task<GetBookMetadataFixture> CreateAsync()
    {
        var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<GetBookMetadataTestDbContext>()
            .UseSqlite(connection)
            .Options;
        var context = new GetBookMetadataTestDbContext(options);
        await context.Database.EnsureCreatedAsync();
        return new GetBookMetadataFixture(connection, context);
    }

    public async ValueTask DisposeAsync()
    {
        await Context.DisposeAsync();
        await connection.DisposeAsync();
    }
}

internal sealed class GetBookMetadataTestDbContext(DbContextOptions<GetBookMetadataTestDbContext> options)
    : DbContext(options), IProjectDbContext
{
    public DbSet<Book> Books => Set<Book>();

    DbSet<Product> IProjectDbContext.Products => throw new NotSupportedException();
    DbSet<User> IProjectDbContext.Users => throw new NotSupportedException();
    DbSet<AssoEvents> IProjectDbContext.AssoEvents => throw new NotSupportedException();
    DbSet<Order> IProjectDbContext.Orders => throw new NotSupportedException();
    DbSet<BookAnnouncement> IProjectDbContext.BookAnnouncements => throw new NotSupportedException();
    DbSet<BookMovement> IProjectDbContext.BookMovements => throw new NotSupportedException();
    DbSet<ScanSession> IProjectDbContext.ScanSessions => throw new NotSupportedException();
    DbSet<AssociationSettings> IProjectDbContext.AssociationSettings => throw new NotSupportedException();
    DbSet<Watchlist> IProjectDbContext.Watchlists => throw new NotSupportedException();
    DbSet<WatchlistItem> IProjectDbContext.WatchlistItems => throw new NotSupportedException();
    DbSet<UserAlertHistory> IProjectDbContext.UserAlertHistories => throw new NotSupportedException();
    DbSet<EmailBounceEvent> IProjectDbContext.EmailBounceEvents => throw new NotSupportedException();
    DbSet<Vole_Papillon_Damour.Domain.RareBookAggregate.RareBook> IProjectDbContext.RareBooks => throw new NotSupportedException();
    DbSet<Vole_Papillon_Damour.Domain.RareBookAggregate.Entities.RareBookPhoto> IProjectDbContext.RareBookPhotos => throw new NotSupportedException();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Ignore<Vole_Papillon_Damour.Domain.RareBookAggregate.RareBook>();
        modelBuilder.Ignore<Vole_Papillon_Damour.Domain.RareBookAggregate.Entities.RareBookPhoto>();
        modelBuilder.Ignore<Product>();
        modelBuilder.Ignore<User>();
        modelBuilder.Ignore<AssoEvents>();
        modelBuilder.Ignore<Order>();
        modelBuilder.Ignore<BookAnnouncement>();
        modelBuilder.Ignore<BookMovement>();
        modelBuilder.Ignore<ScanSession>();
        modelBuilder.Ignore<AssociationSettings>();
        modelBuilder.Ignore<Watchlist>();
        modelBuilder.Ignore<WatchlistItem>();
        modelBuilder.Ignore<UserAlertHistory>();
        modelBuilder.Ignore<EmailBounceEvent>();

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
            builder.Property(book => book.MetadataSource).HasConversion<byte?>();
            builder.Property(book => book.CoverSource).HasConversion<byte?>();
        });
    }

    private static Isbn13 ParseIsbn(string value)
    {
        Isbn13.TryCreate(value, out var isbn13);
        return isbn13;
    }
}
