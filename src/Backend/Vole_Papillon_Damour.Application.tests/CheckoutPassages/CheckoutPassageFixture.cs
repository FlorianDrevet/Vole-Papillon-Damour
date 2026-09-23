using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using Microsoft.Extensions.Logging.Abstractions;
using Vole_Papillon_Damour.Application.CheckoutPassages.Common;
using Vole_Papillon_Damour.Application.Common.Interfaces.Persistence;
using Vole_Papillon_Damour.Domain.AssoEventsAggregate;
using Vole_Papillon_Damour.Domain.AssociationSettingsAggregate;
using Vole_Papillon_Damour.Domain.BookAggregate;
using Vole_Papillon_Damour.Domain.BookAggregate.Entities;
using Vole_Papillon_Damour.Domain.BookAggregate.ValueObjects;
using Vole_Papillon_Damour.Domain.BookMovementAggregate;
using Vole_Papillon_Damour.Domain.BookMovementAggregate.ValueObjects;
using Vole_Papillon_Damour.Domain.CheckoutPassageAggregate;
using Vole_Papillon_Damour.Domain.Common.Models;
using Vole_Papillon_Damour.Domain.MemberCardAggregate;
using Vole_Papillon_Damour.Domain.MemberSelectionAggregate;
using Vole_Papillon_Damour.Domain.OrderAggregate;
using Vole_Papillon_Damour.Domain.ProductAggregate;
using Vole_Papillon_Damour.Domain.RareBookAggregate;
using Vole_Papillon_Damour.Domain.RareBookAggregate.Entities;
using Vole_Papillon_Damour.Domain.RareBookAggregate.ValueObjects;
using Vole_Papillon_Damour.Domain.ScanSessionAggregate;
using Vole_Papillon_Damour.Domain.UserAggregate;
using Vole_Papillon_Damour.Domain.UserAggregate.ValueObjects;
using Vole_Papillon_Damour.Domain.WatchlistAggregate;
using Vole_Papillon_Damour.Infrastructure.Persistence.Configurations;

namespace Vole_Papillon_Damour.Application.tests.CheckoutPassages;

internal sealed class CheckoutPassageFixture : IAsyncDisposable
{
    private readonly SqliteConnection _connection;
    private readonly DateTime _now;
    private readonly Dictionary<string, string> _redirects = new(StringComparer.Ordinal);

    private CheckoutPassageFixture(SqliteConnection connection, CheckoutPassageTestDbContext context, DateTime now)
    {
        _connection = connection;
        Context = context;
        _now = now;
    }

    public CheckoutPassageTestDbContext Context { get; }

    public static async Task<CheckoutPassageFixture> CreateAsync(DateTime now)
    {
        var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<CheckoutPassageTestDbContext>()
            .UseSqlite(connection)
            .Options;
        var context = new CheckoutPassageTestDbContext(options);
        await context.Database.EnsureCreatedAsync();
        return new CheckoutPassageFixture(connection, context, now);
    }

    public CheckoutPassageRecorder CreateRecorder() =>
        new(Context, NullLogger<CheckoutPassageRecorder>.Instance);

    public async Task<User> AddMemberAsync()
    {
        var userId = UserId.Create(Guid.NewGuid());
        var member = User.CreateFromExternalIdentity(
            userId,
            userId.Value.ToString("N"),
            $"member-{userId.Value:N}@example.test",
            _now);
        Context.Users.Add(member);
        await Context.SaveChangesAsync();
        return member;
    }

    public async Task<MemberSelectionItem> AddSelectionAsync(User member, string isbnValue)
    {
        var selection = MemberSelectionItem.CreateForEdition(
            Guid.NewGuid(), member.Id, ParseIsbn(isbnValue), _now);
        Context.MemberSelectionItems.Add(selection);
        await Context.SaveChangesAsync();
        return selection;
    }

    public async Task<MemberSelectionItem> AddSelectionForRareBookAsync(User member, RareBook rareBook)
    {
        var selection = MemberSelectionItem.CreateForRareBook(
            Guid.NewGuid(), member.Id, rareBook.Id, _now);
        Context.MemberSelectionItems.Add(selection);
        await Context.SaveChangesAsync();
        return selection;
    }

    public async Task<(Book Book, BookMovement Movement)> AddSaleAsync(string isbnValue, Guid passageId)
    {
        var isbn = ParseIsbn(_redirects.GetValueOrDefault(isbnValue, isbnValue));
        var book = Book.Create(isbn, _now);
        var movement = CreateMovement(isbn, passageId);
        Context.BookMovements.Add(movement);
        await Context.SaveChangesAsync();
        return (book, movement);
    }

    public async Task<CheckoutPassage> AddAssociatedPassageWithSaleAsync(User member, string requestedIsbn)
    {
        var passageId = Guid.NewGuid();
        var (book, movement) = await AddSaleAsync(requestedIsbn, passageId);
        var passage = CheckoutPassage.OpenFromSale(passageId, _now, _now);
        passage.Associate(member.Id, member.Id, _now);
        Context.CheckoutPassages.Add(passage);
        Context.CheckoutPassageLines.Add(CheckoutPassageLine.ForOrdinarySale(
            Guid.NewGuid(), passageId, movement, book, requestedIsbn));
        await Context.SaveChangesAsync();
        return passage;
    }

    public async Task<CheckoutPassage> AddPendingPassageWithSaleAsync(string isbnValue)
    {
        var passageId = Guid.NewGuid();
        var (book, movement) = await AddSaleAsync(isbnValue, passageId);
        var passage = CheckoutPassage.OpenFromSale(passageId, _now, _now);
        Context.CheckoutPassages.Add(passage);
        Context.CheckoutPassageLines.Add(CheckoutPassageLine.ForOrdinarySale(
            Guid.NewGuid(), passageId, movement, book, isbnValue));
        await Context.SaveChangesAsync();
        return passage;
    }

    public Task RedirectBookAsync(string from, string to)
    {
        _redirects[from] = to;
        return Task.CompletedTask;
    }

    public async Task AddVoidedLineAsync(CheckoutPassage passage, string isbnValue)
    {
        var (book, movement) = await AddSaleAsync(isbnValue, passage.Id);
        var line = CheckoutPassageLine.ForOrdinarySale(
            Guid.NewGuid(), passage.Id, movement, book, isbnValue);
        line.Void(_now);
        Context.CheckoutPassageLines.Add(line);
        await Context.SaveChangesAsync();
    }

    public RareBook CreateRareBook(UserId createdBy) =>
        RareBook.Create("Édition rare de test", 25m, _now, createdBy);

    public async Task<MemberSelectionItem> Reload(MemberSelectionItem selection)
    {
        await Context.Entry(selection).ReloadAsync();
        return selection;
    }

    public async ValueTask DisposeAsync()
    {
        await Context.DisposeAsync();
        await _connection.DisposeAsync();
    }

    private BookMovement CreateMovement(Isbn13 isbn, Guid passageId) => BookMovement.Create(
        BookMovementId.Create(Guid.NewGuid()),
        isbn,
        BookMovementType.Sale,
        -1,
        _now,
        _now,
        clockSuspect: false,
        scanSessionId: null,
        volunteerId: null,
        assoEventsId: null,
        note: null,
        clientGestureId: null,
        checkoutPassageId: passageId);

    private static Isbn13 ParseIsbn(string value) => Isbn13.TryCreate(value, out var isbn)
        ? isbn
        : throw new InvalidOperationException($"Invalid test ISBN: {value}");
}

internal sealed class CheckoutPassageTestDbContext(DbContextOptions<CheckoutPassageTestDbContext> options)
    : DbContext(options), IProjectDbContext
{
    public DbSet<User> Users => Set<User>();
    public DbSet<BookMovement> BookMovements => Set<BookMovement>();
    public DbSet<MemberSelectionItem> MemberSelectionItems => Set<MemberSelectionItem>();
    public DbSet<CheckoutPassage> CheckoutPassages => Set<CheckoutPassage>();
    public DbSet<CheckoutPassageLine> CheckoutPassageLines => Set<CheckoutPassageLine>();

    DbSet<Product> IProjectDbContext.Products => throw new NotSupportedException();
    DbSet<AssoEvents> IProjectDbContext.AssoEvents => throw new NotSupportedException();
    DbSet<Order> IProjectDbContext.Orders => throw new NotSupportedException();
    DbSet<Book> IProjectDbContext.Books => throw new NotSupportedException();
    DbSet<BookAnnouncement> IProjectDbContext.BookAnnouncements => throw new NotSupportedException();
    DbSet<ScanSession> IProjectDbContext.ScanSessions => throw new NotSupportedException();
    DbSet<AssociationSettings> IProjectDbContext.AssociationSettings => throw new NotSupportedException();
    DbSet<Watchlist> IProjectDbContext.Watchlists => throw new NotSupportedException();
    DbSet<WatchlistItem> IProjectDbContext.WatchlistItems => throw new NotSupportedException();
    DbSet<UserAlertHistory> IProjectDbContext.UserAlertHistories => throw new NotSupportedException();
    DbSet<EmailBounceEvent> IProjectDbContext.EmailBounceEvents => throw new NotSupportedException();
    DbSet<RareBook> IProjectDbContext.RareBooks => throw new NotSupportedException();
    DbSet<RareBookPhoto> IProjectDbContext.RareBookPhotos => throw new NotSupportedException();
    DbSet<MemberCard> IProjectDbContext.MemberCards => throw new NotSupportedException();
    DbSet<RareBookTombstone> IProjectDbContext.RareBookTombstones => throw new NotSupportedException();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Ignore<Product>();
        modelBuilder.Ignore<AssoEvents>();
        modelBuilder.Ignore<Order>();
        modelBuilder.Ignore<Book>();
        modelBuilder.Ignore<BookAnnouncement>();
        modelBuilder.Ignore<ScanSession>();
        modelBuilder.Ignore<AssociationSettings>();
        modelBuilder.Ignore<Watchlist>();
        modelBuilder.Ignore<WatchlistItem>();
        modelBuilder.Ignore<UserAlertHistory>();
        modelBuilder.Ignore<EmailBounceEvent>();
        modelBuilder.Ignore<RareBook>();
        modelBuilder.Ignore<RareBookPhoto>();
        modelBuilder.Ignore<MemberCard>();
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

        modelBuilder.Entity<BookMovement>(builder =>
        {
            builder.HasKey(movement => movement.Id);
            builder.Property(movement => movement.Id)
                .ValueGeneratedNever()
                .HasConversion(id => id.Value, value => BookMovementId.Create(value));
            builder.Property(movement => movement.Isbn13)
                .HasConversion(isbn => isbn.Value, value => ParseIsbn(value));
            builder.Property(movement => movement.Type).HasConversion<byte>();
            builder.Property(movement => movement.ScanSessionId).HasConversion(new ValueConverter<
                Vole_Papillon_Damour.Domain.ScanSessionAggregate.ValueObjects.ScanSessionId?, Guid?>(
                id => id == null ? null : id.Value,
                value => value.HasValue
                    ? Vole_Papillon_Damour.Domain.ScanSessionAggregate.ValueObjects.ScanSessionId.Create(value.Value)
                    : null));
            builder.Property(movement => movement.VolunteerId).HasConversion(new ValueConverter<UserId?, Guid?>(
                id => id == null ? null : id.Value,
                value => value.HasValue ? UserId.Create(value.Value) : null));
            builder.Property(movement => movement.AssoEventsId).HasConversion(new ValueConverter<
                Vole_Papillon_Damour.Domain.EventsAggregate.ValueObjects.AssoEventsId?, Guid?>(
                id => id == null ? null : id.Value,
                value => value.HasValue
                    ? Vole_Papillon_Damour.Domain.EventsAggregate.ValueObjects.AssoEventsId.Create(value.Value)
                    : null));
            builder.Property(movement => movement.ReversalOfMovementId).HasConversion(new ValueConverter<
                BookMovementId?, Guid?>(
                id => id == null ? null : id.Value,
                value => value.HasValue ? BookMovementId.Create(value.Value) : null));
            builder.Property(movement => movement.CheckoutPassageId);
        });

        modelBuilder.ApplyConfiguration(new MemberSelectionItemConfiguration());
        modelBuilder.ApplyConfiguration(new CheckoutPassageConfiguration());
        modelBuilder.ApplyConfiguration(new CheckoutPassageLineConfiguration());
    }

    private static Isbn13 ParseIsbn(string value) => Isbn13.TryCreate(value, out var isbn)
        ? isbn
        : throw new InvalidOperationException($"Invalid test ISBN: {value}");
}
