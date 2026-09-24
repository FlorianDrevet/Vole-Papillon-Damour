using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using Vole_Papillon_Damour.Application.Common.Interfaces.Services;
using Vole_Papillon_Damour.Application.Common.Services;
using Vole_Papillon_Damour.Application.MemberSelection.Commands.AddSelectionItem;
using Vole_Papillon_Damour.Application.MemberSelection.Commands.MergeSelection;
using Vole_Papillon_Damour.Application.MemberSelection.Commands.RemoveSelectionItem;
using Vole_Papillon_Damour.Application.MemberSelection.Queries.GetMySelection;
using Vole_Papillon_Damour.Application.NotFoundReports.Commands.CancelNotFoundReport;
using Vole_Papillon_Damour.Application.NotFoundReports.Commands.ReportNotFound;
using Vole_Papillon_Damour.Application.Common.Interfaces.Persistence;
using Vole_Papillon_Damour.Domain.AssoEventsAggregate;
using Vole_Papillon_Damour.Domain.AssoEventsAggregate.ValueObjects;
using Vole_Papillon_Damour.Domain.AssociationSettingsAggregate;
using Vole_Papillon_Damour.Domain.BookAggregate;
using Vole_Papillon_Damour.Domain.BookAggregate.Entities;
using Vole_Papillon_Damour.Domain.BookAggregate.ValueObjects;
using Vole_Papillon_Damour.Domain.BookMovementAggregate;
using Vole_Papillon_Damour.Domain.BookMovementAggregate.ValueObjects;
using Vole_Papillon_Damour.Domain.Common.Models;
using Vole_Papillon_Damour.Domain.EventsAggregate.ValueObjects;
using Vole_Papillon_Damour.Domain.MemberSelectionAggregate;
using Vole_Papillon_Damour.Domain.NotFoundReportAggregate;
using Vole_Papillon_Damour.Domain.NotFoundReportAggregate.ValueObjects;
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
using Vole_Papillon_Damour.Infrastructure.Persistence.Configurations;

namespace Vole_Papillon_Damour.Application.tests.MemberSelection;

internal sealed class MemberSelectionFixture : IAsyncDisposable
{
    private readonly SqliteConnection _connection;
    private readonly MemberSelectionTestClock _clock;
    private readonly DateTime _now;

    private MemberSelectionFixture(
        SqliteConnection connection,
        MemberSelectionTestDbContext context,
        DateTime now)
    {
        _connection = connection;
        Context = context;
        _now = now;
        _clock = new MemberSelectionTestClock(now);
    }

    public MemberSelectionTestDbContext Context { get; }
    public MemberSelectionTestClock Clock => _clock;

    public static async Task<MemberSelectionFixture> CreateAsync(
        DateTime now,
        DbCommandInterceptor? commandInterceptor = null)
    {
        var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var optionsBuilder = new DbContextOptionsBuilder<MemberSelectionTestDbContext>()
            .UseSqlite(connection);
        if (commandInterceptor is not null)
        {
            optionsBuilder.AddInterceptors(commandInterceptor);
        }

        var options = optionsBuilder.Options;
        var context = new MemberSelectionTestDbContext(options);
        await context.Database.EnsureCreatedAsync();
        return new MemberSelectionFixture(connection, context, now);
    }

    public MemberIdentityService CreateMemberIdentityService() => new(Context, _clock);

    public AddSelectionItemCommandHandler CreateAddHandler() =>
        new(Context, CreateMemberIdentityService(), _clock);

    public RemoveSelectionItemCommandHandler CreateRemoveHandler() =>
        new(Context, CreateMemberIdentityService());

    public MergeSelectionCommandHandler CreateMergeHandler() =>
        new(Context, CreateMemberIdentityService(), _clock);

    public ReportNotFoundCommandHandler CreateReportNotFoundHandler() =>
        new(Context, CreateMemberIdentityService(), _clock,
            Microsoft.Extensions.Logging.Abstractions.NullLogger<ReportNotFoundCommandHandler>.Instance);

    public CancelNotFoundReportCommandHandler CreateCancelNotFoundReportHandler() =>
        new(Context, CreateMemberIdentityService(), _clock);

    public GetMySelectionQueryHandler CreateGetMySelectionHandler() =>
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

    public async Task HideBookAsync(string isbn)
    {
        var book = await Context.Books.SingleAsync(candidate => candidate.Id == ParseIsbn(isbn));
        book.UpdateCatalogVisibility(true, _clock.UtcNow);
        await Context.SaveChangesAsync();
    }

    public async Task MarkRareBookSoldAsync(RareBookId rareBookId)
    {
        var rareBook = await Context.RareBooks.SingleAsync(candidate => candidate.Id == rareBookId);
        rareBook.MarkSold(_clock.UtcNow);
        await Context.SaveChangesAsync();
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

    public async Task<AssoEvents> AddFairAsync(DateTimeOffset dateStart, DateTimeOffset? dateEnd = null)
    {
        var fair = AssoEvents.Create(
            "Test book fair",
            null,
            new EventsType(EventsType.EventsTypeEnum.Books),
            dateStart,
            dateEnd,
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

    public async Task<BookNotFoundReport> AddNotFoundReportAsync(
        UserId userId,
        string isbn,
        DateTime reportedAt)
    {
        var report = BookNotFoundReport.CreateForEdition(
            Guid.NewGuid(),
            userId,
            ParseIsbn(isbn),
            NotFoundReportLocation.Fair,
            null,
            reportedAt);
        Context.BookNotFoundReports.Add(report);
        await Context.SaveChangesAsync();
        return report;
    }

    public async Task SetNotFoundReportDailyLimitAsync(UserId updatedBy, int dailyLimit)
    {
        var settings = AssociationSettings.Create(updatedBy, _now);
        settings.Update(
            settings.DuplicateThreshold,
            settings.DemandSalesThreshold,
            settings.DeadStockMinAgeDays,
            settings.DeadStockMinQuantity,
            settings.WatchlistMaxItems,
            settings.AlertCooldownDays,
            settings.SessionIdleTimeoutMinutes,
            settings.AlertDelayMinutes,
            dailyLimit,
            updatedBy,
            _now);
        Context.AssociationSettings.Add(settings);
        await Context.SaveChangesAsync();
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

internal sealed class MemberSelectionTestClock(DateTime utcNow) : IDateTimeProvider
{
    public DateTime UtcNow { get; private set; } = utcNow;

    public void Advance(TimeSpan elapsed) => UtcNow = UtcNow.Add(elapsed);
}

internal sealed class MemberSelectionTestDbContext(DbContextOptions<MemberSelectionTestDbContext> options)
    : DbContext(options), IProjectDbContext
{
    public DbSet<User> Users => Set<User>();
    public DbSet<Book> Books => Set<Book>();
    public DbSet<BookMovement> BookMovements => Set<BookMovement>();
    public DbSet<BookAnnouncement> BookAnnouncements => Set<BookAnnouncement>();
    public DbSet<RareBook> RareBooks => Set<RareBook>();
    public DbSet<RareBookPhoto> RareBookPhotos => Set<RareBookPhoto>();
    public DbSet<AssoEvents> AssoEvents => Set<AssoEvents>();
    public DbSet<WatchlistItem> WatchlistItems => Set<WatchlistItem>();
    public DbSet<MemberSelectionItem> MemberSelectionItems => Set<MemberSelectionItem>();
    public DbSet<AssociationSettings> AssociationSettings => Set<AssociationSettings>();
    public DbSet<BookNotFoundReport> BookNotFoundReports => Set<BookNotFoundReport>();

    DbSet<Product> IProjectDbContext.Products => throw new NotSupportedException();
    DbSet<Order> IProjectDbContext.Orders => throw new NotSupportedException();
    DbSet<ScanSession> IProjectDbContext.ScanSessions => throw new NotSupportedException();
    DbSet<Watchlist> IProjectDbContext.Watchlists => throw new NotSupportedException();
    DbSet<UserAlertHistory> IProjectDbContext.UserAlertHistories => throw new NotSupportedException();
    DbSet<EmailBounceEvent> IProjectDbContext.EmailBounceEvents => throw new NotSupportedException();
    DbSet<RareBookPhoto> IProjectDbContext.RareBookPhotos => RareBookPhotos;
    DbSet<RareBookTombstone> IProjectDbContext.RareBookTombstones => throw new NotSupportedException();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Ignore<Product>();
        modelBuilder.Ignore<Order>();
        modelBuilder.Ignore<ScanSession>();
        modelBuilder.Ignore<Watchlist>();
        modelBuilder.Ignore<UserAlertHistory>();
        modelBuilder.Ignore<EmailBounceEvent>();
        modelBuilder.Ignore<RareBookTombstone>();

        modelBuilder.ApplyConfiguration(new AssociationSettingsConfiguration());
        modelBuilder.ApplyConfiguration(new BookNotFoundReportConfiguration());

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

        modelBuilder.Entity<BookMovement>(builder =>
        {
            builder.HasKey(movement => movement.Id);
            builder.Property(movement => movement.Id)
                .ValueGeneratedNever()
                .HasConversion(id => id.Value, value => BookMovementId.Create(value));
            builder.Property(movement => movement.Isbn13)
                .HasConversion(isbn => isbn.Value, value => ParseIsbn(value));
            builder.Property(movement => movement.Type).HasConversion<byte>();
            builder.Property(movement => movement.ScanSessionId)
                .HasConversion(new ValueConverter<ScanSessionId?, Guid?>(
                    id => id == null ? null : id.Value,
                    value => value.HasValue ? ScanSessionId.Create(value.Value) : null));
            builder.Property(movement => movement.VolunteerId)
                .HasConversion(new ValueConverter<UserId?, Guid?>(
                    id => id == null ? null : id.Value,
                    value => value.HasValue ? UserId.Create(value.Value) : null));
            builder.Property(movement => movement.AssoEventsId)
                .HasConversion(new ValueConverter<AssoEventsId?, Guid?>(
                    id => id == null ? null : id.Value,
                    value => value.HasValue ? AssoEventsId.Create(value.Value) : null));
            builder.Property(movement => movement.ReversalOfMovementId)
                .HasConversion(new ValueConverter<BookMovementId?, Guid?>(
                    id => id == null ? null : id.Value,
                    value => value.HasValue ? BookMovementId.Create(value.Value) : null));
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
            builder.Property(book => book.Slug)
                .HasConversion(slug => slug.Value, value => RareBookSlug.Create(value));
            builder.Property(book => book.Isbn13)
                .HasConversion(new ValueConverter<Isbn13?, string?>(
                    isbn => isbn == null ? null : isbn.Value.Value,
                    value => value == null ? null : ParseIsbn(value)));
            builder.Property(book => book.Status).HasConversion<byte>();
            builder.Ignore(book => book.Price);
            builder.Ignore(book => book.Condition);
            builder.Ignore(book => book.PublicDescription);
            builder.Ignore(book => book.SoldAtFairId);
            builder.Ignore(book => book.SoldInSessionId);
            builder.Ignore(book => book.CreatedBy);
            builder.Ignore(book => book.UpdatedBy);
            builder.Ignore(book => book.RowVersion);
            builder.Ignore(book => book.ClientGestureId);
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

        modelBuilder.Entity<AssoEvents>(builder =>
        {
            builder.HasKey(assoEvent => assoEvent.Id);
            builder.Property(assoEvent => assoEvent.Id)
                .ValueGeneratedNever()
                .HasConversion(id => id.Value, value => AssoEventsId.Create(value));
            builder.Property(assoEvent => assoEvent.EventsType)
                .HasConversion(type => (int)type.Value,
                    value => new EventsType((EventsType.EventsTypeEnum)value));
            builder.Ignore(assoEvent => assoEvent.UrlImage);
            builder.Ignore(assoEvent => assoEvent.UrlRegistration);
            builder.Ignore(assoEvent => assoEvent.UrlImageMap);
            builder.Ignore(assoEvent => assoEvent.HourOpenDoors);
            builder.Ignore(assoEvent => assoEvent.HourCloseDoors);
            builder.Ignore(assoEvent => assoEvent.Adresse);
            builder.Ignore(assoEvent => assoEvent.BingoHasBeenWon);
            builder.Ignore(assoEvent => assoEvent.BookRevenue);
            builder.Ignore(assoEvent => assoEvent.CurrentPartieIndex);
            builder.Ignore(assoEvent => assoEvent.Parties);
            builder.Ignore(assoEvent => assoEvent.BingoNumeros);
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
