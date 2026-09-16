using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using NSubstitute;
using Vole_Papillon_Damour.Application.Common.Interfaces.Persistence;
using Vole_Papillon_Damour.Application.Common.Interfaces.Services;
using Vole_Papillon_Damour.Application.RareBooks.Commands.CreateRareBook;
using Vole_Papillon_Damour.Application.RareBooks.Commands.UpdateRareBook;
using Vole_Papillon_Damour.Domain.AssoEventsAggregate;
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

namespace Vole_Papillon_Damour.Application.tests.RareBooks;

internal sealed class RareBookFeatureTestFixture : IAsyncDisposable
{
    public static readonly DateTime DefaultNow =
        new(2026, 9, 16, 12, 0, 0, DateTimeKind.Utc);

    private readonly SqliteConnection connection;

    private RareBookFeatureTestFixture(
        SqliteConnection connection,
        RareBookFeatureTestDbContext context)
    {
        this.connection = connection;
        Context = context;
        Clock = Substitute.For<IDateTimeProvider>();
        Clock.UtcNow.Returns(Now);
        Blob = Substitute.For<IBlobService>();
    }

    public RareBookFeatureTestDbContext Context { get; }
    public IDateTimeProvider Clock { get; }
    public IBlobService Blob { get; }
    public UserId UserId { get; } = UserId.Create(Guid.Parse("00000000-0000-0000-0000-000000000001"));
    public DateTime Now => DefaultNow;

    public static async Task<RareBookFeatureTestFixture> CreateAsync()
    {
        var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<RareBookFeatureTestDbContext>()
            .UseSqlite(connection)
            .Options;
        var context = new RareBookFeatureTestDbContext(options);
        await context.Database.EnsureCreatedAsync();
        return new RareBookFeatureTestFixture(connection, context);
    }

    public CreateRareBookCommand CreateCommand(
        string title = "Livre rare",
        decimal price = 25m,
        string? isbn13 = null) =>
        new(
            title,
            "Un auteur",
            "Un éditeur",
            1920,
            RareBookShelf.AncientEditions.Value,
            price,
            nameof(RareBookCondition.RareBookConditionEnum.GoodWithFlaws),
            "Description",
            "Reliure",
            null,
            120,
            "A-01",
            "Bénévole",
            isbn13,
            UserId);

    public UpdateRareBookCommand UpdateCommand(
        RareBookId id,
        string title,
        byte[] rowVersion) =>
        new(
            id,
            title,
            "Un auteur",
            "Un éditeur",
            1920,
            RareBookShelf.AncientEditions.Value,
            25m,
            nameof(RareBookCondition.RareBookConditionEnum.GoodWithFlaws),
            "Description",
            "Reliure",
            null,
            120,
            "A-01",
            "Bénévole",
            null,
            rowVersion,
            UserId);

    public async Task<RareBook> AddRareBookAsync(
        string title,
        decimal price = 25m,
        bool published = false,
        string? isbn13 = null)
    {
        Isbn13? parsedIsbn13 = null;
        if (!string.IsNullOrWhiteSpace(isbn13))
        {
            if (!Isbn13.TryCreate(isbn13, out var parsed))
            {
                throw new ArgumentException("The test ISBN is invalid.", nameof(isbn13));
            }

            parsedIsbn13 = parsed;
        }

        var book = RareBook.Create(
            title,
            price,
            Now,
            UserId,
            authorMention: "Un auteur",
            publisher: "Un éditeur",
            publicationYear: 1920,
            shelf: RareBookShelf.AncientEditions,
            condition: RareBookCondition.GoodWithFlaws,
            isbn13: parsedIsbn13);
        if (published)
        {
            book.Publish(UserId, Now.AddMinutes(1));
        }

        Context.RareBooks.Add(book);
        await Context.SaveChangesAsync();
        return book;
    }

    public async Task<RareBookPhoto> AddPhotoAsync(
        RareBook book,
        string blobName = "photo.jpg")
    {
        var photo = RareBookPhoto.Create(
            book.Id,
            new Uri($"https://storage.test/{book.Id.Value:D}/{blobName}"),
            $"{book.Id.Value:D}/{blobName}",
            null,
            book.Photos.Count,
            "image/jpeg",
            100,
            Now,
            UserId);
        book.AddPhoto(photo, Now, UserId);
        Context.RareBookPhotos.Add(photo);
        await Context.SaveChangesAsync();
        return photo;
    }

    public async ValueTask DisposeAsync()
    {
        await Context.DisposeAsync();
        await connection.DisposeAsync();
    }
}

internal sealed class RareBookFeatureTestDbContext(
    DbContextOptions<RareBookFeatureTestDbContext> options)
    : DbContext(options), IProjectDbContext
{
    public DbSet<RareBook> RareBooks => Set<RareBook>();
    public DbSet<RareBookPhoto> RareBookPhotos => Set<RareBookPhoto>();

    DbSet<Product> IProjectDbContext.Products => throw new NotSupportedException();
    DbSet<User> IProjectDbContext.Users => throw new NotSupportedException();
    DbSet<AssoEvents> IProjectDbContext.AssoEvents => throw new NotSupportedException();
    DbSet<Order> IProjectDbContext.Orders => throw new NotSupportedException();
    DbSet<Book> IProjectDbContext.Books => throw new NotSupportedException();
    DbSet<BookAnnouncement> IProjectDbContext.BookAnnouncements => throw new NotSupportedException();
    DbSet<BookMovement> IProjectDbContext.BookMovements => throw new NotSupportedException();
    DbSet<ScanSession> IProjectDbContext.ScanSessions => throw new NotSupportedException();
    DbSet<AssociationSettings> IProjectDbContext.AssociationSettings => throw new NotSupportedException();
    DbSet<Watchlist> IProjectDbContext.Watchlists => throw new NotSupportedException();
    DbSet<WatchlistItem> IProjectDbContext.WatchlistItems => throw new NotSupportedException();
    DbSet<UserAlertHistory> IProjectDbContext.UserAlertHistories => throw new NotSupportedException();
    DbSet<EmailBounceEvent> IProjectDbContext.EmailBounceEvents => throw new NotSupportedException();
    DbSet<RareBook> IProjectDbContext.RareBooks => RareBooks;
    DbSet<RareBookPhoto> IProjectDbContext.RareBookPhotos => RareBookPhotos;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Ignore<Product>();
        modelBuilder.Ignore<User>();
        modelBuilder.Ignore<AssoEvents>();
        modelBuilder.Ignore<Order>();
        modelBuilder.Ignore<Book>();
        modelBuilder.Ignore<BookAnnouncement>();
        modelBuilder.Ignore<BookMovement>();
        modelBuilder.Ignore<ScanSession>();
        modelBuilder.Ignore<AssociationSettings>();
        modelBuilder.Ignore<Watchlist>();
        modelBuilder.Ignore<WatchlistItem>();
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
    }

    private static Isbn13 ParseIsbn(string value) =>
        Isbn13.TryCreate(value, out var isbn)
            ? isbn
            : throw new InvalidOperationException($"Invalid test ISBN: {value}");
}
