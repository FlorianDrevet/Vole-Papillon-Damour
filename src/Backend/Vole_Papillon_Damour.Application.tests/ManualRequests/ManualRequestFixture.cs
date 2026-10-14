using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Vole_Papillon_Damour.Application.Books.Common;
using Vole_Papillon_Damour.Application.Common.Interfaces.Services;
using Vole_Papillon_Damour.Domain.AssoEventsAggregate;
using Vole_Papillon_Damour.Domain.AssoEventsAggregate.ValueObjects;
using Vole_Papillon_Damour.Domain.BookAggregate;
using Vole_Papillon_Damour.Domain.BookAggregate.Entities;
using Vole_Papillon_Damour.Domain.BookAggregate.ValueObjects;
using Vole_Papillon_Damour.Domain.BookMovementAggregate;
using Vole_Papillon_Damour.Domain.EventsAggregate.ValueObjects;
using Vole_Papillon_Damour.Domain.ManualRequestAggregate;
using Vole_Papillon_Damour.Domain.ManualRequestAggregate.ValueObjects;
using Vole_Papillon_Damour.Domain.ScanSessionAggregate;
using Vole_Papillon_Damour.Domain.ScanSessionAggregate.ValueObjects;
using Vole_Papillon_Damour.Domain.RareBookAggregate;
using Vole_Papillon_Damour.Domain.UserAggregate;
using Vole_Papillon_Damour.Domain.UserAggregate.ValueObjects;
using Vole_Papillon_Damour.Domain.WatchlistAggregate;
using Vole_Papillon_Damour.Domain.WatchlistAggregate.ValueObjects;
using Vole_Papillon_Damour.Infrastructure.Persistence;

namespace Vole_Papillon_Damour.Application.tests.ManualRequests;

internal sealed class ManualRequestFixture : IAsyncDisposable
{
    private readonly SqliteConnection _connection;
    private readonly DateTime _now;

    private ManualRequestFixture(SqliteConnection connection, ManualRequestTestDbContext context, DateTime now)
    {
        _connection = connection;
        Context = context;
        _now = now;
    }

    public const string DefaultWorkId = "OL123W";
    public static readonly DateTime DefaultNow = new(2026, 10, 12, 10, 0, 0, DateTimeKind.Utc);
    public static readonly UserId AdministratorId = UserId.Create(Guid.Parse("00000000-0000-0000-0000-0000000000a1"));
    public static readonly UserId VolunteerId = UserId.Create(Guid.Parse("00000000-0000-0000-0000-0000000000b2"));

    public ManualRequestTestDbContext Context { get; }

    public static async Task<ManualRequestFixture> CreateAsync(DateTime? now = null)
    {
        var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        connection.CreateCollation(
            "Latin1_General_100_CI_AI",
            (left, right) => string.Compare(left, right, StringComparison.OrdinalIgnoreCase));
        var options = new DbContextOptionsBuilder<ProjectDbContext>()
            .UseSqlite(connection)
            .Options;
        var context = new ManualRequestTestDbContext(options);
        await context.Database.EnsureCreatedAsync();
        var fixture = new ManualRequestFixture(connection, context, now ?? DefaultNow);
        context.Users.AddRange(
            fixture.CreateUser(AdministratorId, "admin@example.test"),
            fixture.CreateUser(VolunteerId, "volunteer@example.test"));
        await context.SaveChangesAsync();
        return fixture;
    }

    public IDateTimeProvider CreateClock(DateTime? now = null) => new ManualRequestTestClock(now ?? _now);

    public async Task<Book> AddBookAsync(string isbn, int quantityAvailable = 0, string? workId = null, string? title = "Le Petit Prince")
    {
        var book = Book.Create(ParseIsbn(isbn), _now);
        if (quantityAvailable > 0)
        {
            book.RecordAvailableEntry(_now, quantityAvailable);
        }

        var fields = new List<BookMetadataField>();
        if (title is not null) fields.Add(BookMetadataField.Title);
        if (workId is not null) fields.Add(BookMetadataField.WorkId);
        if (fields.Count > 0)
        {
            book.ApplyAutomaticMetadata(
                new BookMetadataPatch(title, null, null, null, null, null, null, null, fields, workId),
                BookMetadataSource.OpenLibrary,
                _now,
                rawPayload: null);
        }

        Context.Books.Add(book);
        await Context.SaveChangesAsync();
        return book;
    }

    public async Task<AssoEvents> AddFairAsync(DateTimeOffset? start = null, DateTimeOffset? end = null)
    {
        var fairStart = start ?? new DateTimeOffset(_now.AddDays(7));
        var fair = AssoEvents.Create(
            "Test book fair",
            null,
            new EventsType(EventsType.EventsTypeEnum.Books),
            fairStart,
            end ?? fairStart.AddDays(1),
            null,
            null,
            null,
            new Adresse(null, "Paris", "Rue de test", 75000),
            null,
            [],
            string.Empty);
        Context.AssoEvents.Add(fair);
        await Context.SaveChangesAsync();
        return fair;
    }

    public async Task<ScanSession> AddSessionAsync(ScanMode mode = ScanMode.AvailableNow, AssoEventsId? fairId = null)
    {
        var volunteerId = UserId.CreateUnique();
        Context.Users.Add(CreateUser(volunteerId, $"{volunteerId.Value:N}@example.test"));
        await Context.SaveChangesAsync();
        var session = ScanSession.Create(volunteerId, mode, fairId, _now);
        Context.ScanSessions.Add(session);
        await Context.SaveChangesAsync();
        return session;
    }

    public async Task<BookAnnouncement> AddAnnouncementAsync(string isbn, AssoEventsId? fairId, int quantity)
    {
        var session = await AddSessionAsync(ScanMode.NextFair, fairId);
        var announcement = BookAnnouncement.Create(
            BookAnnouncementId.CreateUnique(), ParseIsbn(isbn), fairId, quantity,
            _now, session.Id, clientGestureId: null);
        Context.BookAnnouncements.Add(announcement);
        await Context.SaveChangesAsync();
        return announcement;
    }

    public async Task<ManualRequester> AddRequesterAsync(string lastName = "Martin", string firstName = "Jeanne", string phone = "0612345678")
    {
        var requester = ManualRequester.Create(Guid.NewGuid(), lastName, firstName, phone, true, AdministratorId, _now);
        Context.ManualRequesters.Add(requester);
        await Context.SaveChangesAsync();
        return requester;
    }

    public async Task<ManualBookRequest> AddWorkRequestAsync(Guid requesterId, string workId = DefaultWorkId, DateTime? requestedAt = null)
    {
        var request = ManualBookRequest.CreateWork(
            Guid.NewGuid(), requesterId, workId, "Le Petit Prince", null, null, null, requestedAt ?? _now);
        Context.ManualBookRequests.Add(request);
        await Context.SaveChangesAsync();
        return request;
    }

    public async Task<ManualBookRequest> AddEditionRequestAsync(Guid requesterId, string isbn, DateTime? requestedAt = null)
    {
        var request = ManualBookRequest.CreateEdition(
            Guid.NewGuid(), requesterId, ParseIsbn(isbn), "Le Petit Prince", null, null, null, requestedAt ?? _now);
        Context.ManualBookRequests.Add(request);
        await Context.SaveChangesAsync();
        return request;
    }

    public async Task<UserId> AddWatcherAsync(WatchlistItemScope scope, string? workId = null, string? isbn = null, bool suspended = false)
    {
        var userId = UserId.CreateUnique();
        Context.Users.Add(CreateUser(userId, $"{userId.Value:N}@example.test"));
        var watchlist = Watchlist.Create(userId, _now);
        if (suspended)
        {
            watchlist.SuspendAlerts(_now.AddMinutes(1));
        }

        Context.Watchlists.Add(watchlist);
        Context.WatchlistItems.Add(scope switch
        {
            WatchlistItemScope.Work => WatchlistItem.CreateWork(Guid.NewGuid(), userId, workId!, _now),
            WatchlistItemScope.Edition => WatchlistItem.CreateEdition(Guid.NewGuid(), userId, ParseIsbn(isbn!), _now),
            _ => throw new ArgumentOutOfRangeException(nameof(scope), scope, null)
        });
        await Context.SaveChangesAsync();
        return userId;
    }

    public static Isbn13 ParseIsbn(string value) => Isbn13.TryCreate(value, out var isbn)
        ? isbn
        : throw new InvalidOperationException($"Invalid test ISBN: {value}");

    private User CreateUser(UserId id, string email) => User.CreateFromExternalIdentity(
        id,
        $"test:{id.Value:N}",
        email,
        _now);

    public async ValueTask DisposeAsync()
    {
        await Context.DisposeAsync();
        await _connection.DisposeAsync();
    }
}

internal sealed class ManualRequestTestClock(DateTime utcNow) : IDateTimeProvider
{
    public DateTime UtcNow { get; private set; } = utcNow;
    public void Advance(TimeSpan elapsed) => UtcNow = UtcNow.Add(elapsed);
}

internal sealed class ManualRequestTestDbContext(DbContextOptions<ProjectDbContext> options)
    : ProjectDbContext(options)
{
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Book>()
            .Property(book => book.RowVersion)
            .ValueGeneratedNever()
            .IsConcurrencyToken(false);
        modelBuilder.Entity<RareBook>()
            .Property(book => book.RowVersion)
            .ValueGeneratedNever()
            .IsConcurrencyToken(false);

        foreach (var property in modelBuilder.Model
                     .GetEntityTypes()
                     .SelectMany(entityType => entityType.GetProperties())
                     .Where(property => property.GetColumnType() == "nvarchar(max)"))
        {
            property.SetColumnType("TEXT");
        }
    }
}
