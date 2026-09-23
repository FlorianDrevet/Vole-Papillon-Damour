using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using NSubstitute;
using Vole_Papillon_Damour.Application.Common.Interfaces.Services;
using Vole_Papillon_Damour.Application.Common.Services;
using Vole_Papillon_Damour.Application.MemberSelection.Commands.AddSelectionItem;
using Vole_Papillon_Damour.Application.MemberSelection.Commands.RemoveSelectionItem;
using Vole_Papillon_Damour.Application.MemberSelection.Commands.SetSelectionItemStatus;
using Vole_Papillon_Damour.Application.Common.Interfaces.Persistence;
using Vole_Papillon_Damour.Domain.AssoEventsAggregate;
using Vole_Papillon_Damour.Domain.AssoEventsAggregate.ValueObjects;
using Vole_Papillon_Damour.Domain.AssociationSettingsAggregate;
using Vole_Papillon_Damour.Domain.BookAggregate;
using Vole_Papillon_Damour.Domain.BookAggregate.Entities;
using Vole_Papillon_Damour.Domain.BookAggregate.ValueObjects;
using Vole_Papillon_Damour.Domain.BookMovementAggregate;
using Vole_Papillon_Damour.Domain.Common.Models;
using Vole_Papillon_Damour.Domain.EventsAggregate.ValueObjects;
using Vole_Papillon_Damour.Domain.MemberSelectionAggregate;
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

namespace Vole_Papillon_Damour.Application.tests.MemberSelection;

internal sealed class MemberSelectionFixture : IAsyncDisposable
{
    private readonly SqliteConnection _connection;
    private readonly IDateTimeProvider _clock;
    private readonly DateTime _now;

    private MemberSelectionFixture(
        SqliteConnection connection,
        MemberSelectionTestDbContext context,
        DateTime now)
    {
        _connection = connection;
        Context = context;
        _now = now;
        _clock = Substitute.For<IDateTimeProvider>();
        _clock.UtcNow.Returns(now);
    }

    public MemberSelectionTestDbContext Context { get; }

    public static async Task<MemberSelectionFixture> CreateAsync(DateTime now)
    {
        var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<MemberSelectionTestDbContext>()
            .UseSqlite(connection)
            .Options;
        var context = new MemberSelectionTestDbContext(options);
        await context.Database.EnsureCreatedAsync();
        return new MemberSelectionFixture(connection, context, now);
    }

    public MemberIdentityService CreateMemberIdentityService() => new(Context, _clock);

    public AddSelectionItemCommandHandler CreateAddHandler() =>
        new(Context, CreateMemberIdentityService(), _clock);

    public RemoveSelectionItemCommandHandler CreateRemoveHandler() =>
        new(Context, CreateMemberIdentityService());

    public SetSelectionItemStatusCommandHandler CreateSetStatusHandler() =>
        new(Context, CreateMemberIdentityService(), _clock);

    public async Task<Book> AddBookAsync(
        string isbn,
        int quantityAvailable = 1,
        bool hidden = false,
        string? redirectTo = null)
    {
        var book = Book.Create(ParseIsbn(isbn), _now);
        if (quantityAvailable > 0)
        {
            book.RecordAvailableEntry(_now, quantityAvailable);
        }

        if (redirectTo is not null)
        {
            book.RedirectTo(ParseIsbn(redirectTo));
        }
        else if (hidden)
        {
            book.UpdateCatalogVisibility(true, _now);
        }

        Context.Books.Add(book);
        await Context.SaveChangesAsync();
        return book;
    }

    public async Task<BookAnnouncement> AddAnnouncementAsync(string isbn)
    {
        var announcement = BookAnnouncement.Create(
            BookAnnouncementId.CreateUnique(),
            ParseIsbn(isbn),
            null,
            1,
            _now,
            ScanSessionId.CreateUnique(),
            Guid.NewGuid());
        Context.BookAnnouncements.Add(announcement);
        await Context.SaveChangesAsync();
        return announcement;
    }

    public async Task<RareBook> AddRareBookAsync(bool published = true, bool sold = false)
    {
        var memberId = UserId.Create(Guid.Parse("7f0b0f0e-0000-0000-0000-00000000c0de"));
        var rareBook = RareBook.Create(
            $"Livre rare {_connection.GetHashCode()}",
            25m,
            _now,
            memberId,
            shelf: RareBookShelf.AncientEditions,
            condition: RareBookCondition.AsNew);
        if (published)
        {
            rareBook.Publish(_now);
        }

        if (sold)
        {
            rareBook.MarkSold(_now.AddMinutes(1));
        }

        Context.RareBooks.Add(rareBook);
        await Context.SaveChangesAsync();
        return rareBook;
    }

    public async Task<MemberSelectionItem> AddSelectionItemForOtherMemberAsync(string isbn)
    {
        var otherMemberId = UserId.CreateUnique();
        var otherMember = User.CreateFromExternalIdentity(
            otherMemberId,
            Guid.NewGuid().ToString(),
            "other@example.test",
            _now);
        Context.Users.Add(otherMember);
        var item = MemberSelectionItem.CreateForEdition(
            Guid.NewGuid(), otherMemberId, ParseIsbn(isbn), _now);
        Context.MemberSelectionItems.Add(item);
        await Context.SaveChangesAsync();
        return item;
    }

    private static Isbn13 ParseIsbn(string value) =>
        Isbn13.TryCreate(value, out var isbn)
            ? isbn
            : throw new InvalidOperationException($"Invalid test ISBN: {value}");

    public async ValueTask DisposeAsync()
    {
        await Context.DisposeAsync();
        await _connection.DisposeAsync();
    }
}

internal sealed class MemberSelectionTestDbContext(DbContextOptions<MemberSelectionTestDbContext> options)
    : DbContext(options), IProjectDbContext
{
    public DbSet<User> Users => Set<User>();
    public DbSet<Book> Books => Set<Book>();
    public DbSet<BookAnnouncement> BookAnnouncements => Set<BookAnnouncement>();
    public DbSet<RareBook> RareBooks => Set<RareBook>();
    public DbSet<WatchlistItem> WatchlistItems => Set<WatchlistItem>();
    public DbSet<MemberSelectionItem> MemberSelectionItems => Set<MemberSelectionItem>();

    DbSet<Product> IProjectDbContext.Products => throw new NotSupportedException();
    DbSet<AssoEvents> IProjectDbContext.AssoEvents => throw new NotSupportedException();
    DbSet<Order> IProjectDbContext.Orders => throw new NotSupportedException();
    DbSet<BookMovement> IProjectDbContext.BookMovements => throw new NotSupportedException();
    DbSet<ScanSession> IProjectDbContext.ScanSessions => throw new NotSupportedException();
    DbSet<AssociationSettings> IProjectDbContext.AssociationSettings => throw new NotSupportedException();
    DbSet<Watchlist> IProjectDbContext.Watchlists => throw new NotSupportedException();
    DbSet<UserAlertHistory> IProjectDbContext.UserAlertHistories => throw new NotSupportedException();
    DbSet<EmailBounceEvent> IProjectDbContext.EmailBounceEvents => throw new NotSupportedException();
    DbSet<RareBookPhoto> IProjectDbContext.RareBookPhotos => throw new NotSupportedException();
    DbSet<RareBookTombstone> IProjectDbContext.RareBookTombstones => throw new NotSupportedException();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Ignore<Product>();
        modelBuilder.Ignore<AssoEvents>();
        modelBuilder.Ignore<Order>();
        modelBuilder.Ignore<BookMovement>();
        modelBuilder.Ignore<ScanSession>();
        modelBuilder.Ignore<AssociationSettings>();
        modelBuilder.Ignore<Watchlist>();
        modelBuilder.Ignore<UserAlertHistory>();
        modelBuilder.Ignore<EmailBounceEvent>();
        modelBuilder.Ignore<RareBookPhoto>();
        modelBuilder.Ignore<RareBookTombstone>();

        modelBuilder.Entity<User>(builder =>
        {
            builder.HasKey(user => user.Id);
            builder.Property(user => user.Id)
                .ValueGeneratedNever()
                .HasConversion(id => id.Value, value => UserId.Create(value));
            builder.Ignore(user => user.Password);
            builder.Ignore(user => user.Salt);
            builder.Ignore(user => user.Role);
            builder.ComplexProperty(user => user.Name);
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
                    (string? value) => value == null ? null : ParseIsbn(value));
            builder.Property(book => book.MetadataStatus).HasConversion<byte>();
            builder.Property(book => book.MetadataSource).HasConversion<byte>();
            builder.Property(book => book.RowVersion)
                .ValueGeneratedNever()
                .IsConcurrencyToken(false);
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

        modelBuilder.Entity<RareBook>(builder =>
        {
            builder.HasKey(book => book.Id);
            builder.Property(book => book.Id)
                .ValueGeneratedNever()
                .HasConversion(id => id.Value, value => RareBookId.Create(value));
            builder.Property(book => book.Status).HasConversion<byte>();
            builder.Ignore(book => book.Slug);
            builder.Ignore(book => book.Isbn13);
            builder.Ignore(book => book.AuthorMention);
            builder.Ignore(book => book.Publisher);
            builder.Ignore(book => book.PublicationYear);
            builder.Ignore(book => book.Shelf);
            builder.Ignore(book => book.Price);
            builder.Ignore(book => book.Condition);
            builder.Ignore(book => book.PublicDescription);
            builder.Ignore(book => book.Binding);
            builder.Ignore(book => book.Dimensions);
            builder.Ignore(book => book.PageCount);
            builder.Ignore(book => book.ShelfLocation);
            builder.Ignore(book => book.SoldAtFairId);
            builder.Ignore(book => book.SoldInSessionId);
            builder.Ignore(book => book.PriceSetBy);
            builder.Ignore(book => book.CreatedBy);
            builder.Ignore(book => book.UpdatedBy);
            builder.Ignore(book => book.Photos);
            builder.Ignore(book => book.RowVersion);
            builder.Ignore(book => book.ClientGestureId);
        });

        modelBuilder.Entity<WatchlistItem>(builder =>
        {
            builder.HasKey(item => item.Id);
            builder.Property(item => item.Id).ValueGeneratedNever();
            builder.Property(item => item.UserId)
                .HasConversion(userId => userId.Value, value => UserId.Create(value));
            builder.Property(item => item.Scope).HasConversion<byte>();
            builder.Property(item => item.Isbn13)
                .HasConversion(new ValueConverter<Isbn13?, string?>(
                    isbn => isbn == null ? null : isbn.Value.Value,
                    value => value == null ? null : ParseIsbn(value)));
            builder.Property(item => item.RareBookId)
                .HasConversion(new ValueConverter<RareBookId?, Guid?>(
                    id => id == null ? null : id.Value,
                    value => value.HasValue ? RareBookId.Create(value.Value) : null));
        });

        modelBuilder.Entity<MemberSelectionItem>(builder =>
        {
            builder.HasKey(item => item.Id);
            builder.Property(item => item.Id).ValueGeneratedNever();
            builder.Property(item => item.UserId)
                .HasConversion(userId => userId.Value, value => UserId.Create(value));
            builder.Property(item => item.Isbn13)
                .HasConversion(new ValueConverter<Isbn13?, string?>(
                    isbn => isbn == null ? null : isbn.Value.Value,
                    value => value == null ? null : ParseIsbn(value)));
            builder.Property(item => item.RareBookId)
                .HasConversion(new ValueConverter<RareBookId?, Guid?>(
                    id => id == null ? null : id.Value,
                    value => value.HasValue ? RareBookId.Create(value.Value) : null));
            builder.Property(item => item.Status).HasConversion<byte>();
        });
    }

    private static Isbn13 ParseIsbn(string value) =>
        Isbn13.TryCreate(value, out var isbn)
            ? isbn
            : throw new InvalidOperationException($"Invalid test ISBN: {value}");
}
