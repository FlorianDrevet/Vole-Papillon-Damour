using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Vole_Papillon_Damour.Application.CheckoutPassages.Commands.AssociateCheckoutPassage;
using Vole_Papillon_Damour.Application.Common.Interfaces.Services;
using Vole_Papillon_Damour.Infrastructure.Services.MemberCards;
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
using Vole_Papillon_Damour.Domain.EventsAggregate.ValueObjects;
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
    // Public deterministic test key only; never use this value for runtime configuration.
    private const string TestSigningKey = "MDEyMzQ1Njc4OWFiY2RlZjAxMjM0NTY3ODlhYmNkZWY=";
    private readonly Dictionary<string, string> _redirects = new(StringComparer.Ordinal);
    private readonly Dictionary<Guid, (Book Book, BookMovement Movement)> _salesByGesture = [];
    private readonly CheckoutPassageTestClock _clock;
    private readonly IMemberCardTokenService _tokens;
    public UserId VolunteerId { get; } = UserId.Create(Guid.Parse("00000000-0000-0000-0000-0000000000f1"));

    private CheckoutPassageFixture(SqliteConnection connection, CheckoutPassageTestDbContext context, DateTime now)
    {
        _connection = connection;
        Context = context;
        _now = now;
        _clock = new CheckoutPassageTestClock(now);
        _tokens = new MemberCardTokenService(Options.Create(new MemberCardTokenOptions { SigningKey = TestSigningKey }));
    }

    public CheckoutPassageTestDbContext Context { get; }

    public bool ThrowWhenRecommendationDataIsRead
    {
        get => Context.ThrowWhenRecommendationDataIsRead;
        set => Context.ThrowWhenRecommendationDataIsRead = value;
    }

    public static async Task<CheckoutPassageFixture> CreateAsync(DateTime now)
    {
        var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        connection.CreateCollation(
            "Latin1_General_100_CI_AI",
            (left, right) => string.Compare(left, right, StringComparison.OrdinalIgnoreCase));
        var options = new DbContextOptionsBuilder<CheckoutPassageTestDbContext>()
            .UseSqlite(connection)
            .Options;
        var context = new CheckoutPassageTestDbContext(options);
        await context.Database.EnsureCreatedAsync();
        return new CheckoutPassageFixture(connection, context, now);
    }

    public CheckoutPassageRecorder CreateRecorder() =>
        new(Context, NullLogger<CheckoutPassageRecorder>.Instance);

    public AssociateCheckoutPassageCommandHandler CreateAssociateHandler() => new(
        Context,
        _clock,
        CreateRecorder(),
        _tokens,
        NullLogger<AssociateCheckoutPassageCommandHandler>.Instance);


    public async Task<User> AddMemberAsync()
    {
        var userId = UserId.Create(Guid.NewGuid());
        var member = User.CreateFromExternalIdentity(
            userId,
            userId.Value.ToString(),
            $"member-{userId.Value:N}@example.test",
            _now);
        Context.Users.Add(member);
        await Context.SaveChangesAsync();
        return member;
    }

    public async Task<Book> AddCatalogBookAsync(
        string isbnValue,
        string title,
        bool available = true,
        string? workId = null)
    {
        var isbn = ParseIsbn(isbnValue);
        var book = Book.Create(isbn, _now.AddMinutes(-10));
        if (available)
        {
            book.RecordAvailableEntry(_now.AddMinutes(-1));
        }

        var fields = workId is null
            ? new[] { BookMetadataField.Title, BookMetadataField.Authors }
            : new[] { BookMetadataField.Title, BookMetadataField.Authors, BookMetadataField.WorkId };
        book.ApplyManualMetadata(
            new BookMetadataPatch(
                title,
                "Auteur test",
                null,
                null,
                null,
                null,
                null,
                null,
                fields,
                workId),
            _now.AddMinutes(-5));
        Context.Books.Add(book);
        await Context.SaveChangesAsync();
        return book;
    }

    public async Task SetCurrentRecommendationGenerationAsync(Guid generationId)
    {
        var generation = await Context.RecommendationGenerations.SingleAsync(candidate => candidate.Id == 1);
        generation.Switch(generationId, 20, _now);
        await Context.SaveChangesAsync();
    }

    public void AddRecommendationNeighbor(
        Guid generationId,
        string sourceIsbn,
        byte rank,
        string neighborIsbn,
        float score,
        Vole_Papillon_Damour.Domain.RecommendationAggregate.ValueObjects.NeighborReason reason) =>
        Context.BookNeighbors.Add(new Vole_Papillon_Damour.Domain.RecommendationAggregate.BookNeighbor(
            generationId, sourceIsbn, rank, neighborIsbn, score, reason));

    public async Task AddRecommendationAnnouncementAsync(string isbnValue, int quantity = 1)
    {
        Context.BookAnnouncements.Add(BookAnnouncement.Create(
            BookAnnouncementId.CreateUnique(),
            ParseIsbn(isbnValue),
            null,
            quantity,
            _now,
            Vole_Papillon_Damour.Domain.ScanSessionAggregate.ValueObjects.ScanSessionId.CreateUnique()));
        await Context.SaveChangesAsync();
    }

    public async Task SetRecommendationPreferenceAsync(User member, bool enabled, DateTime? updatedAt = null)
    {
        Context.MemberRecommendationPreferences.Add(
            Vole_Papillon_Damour.Domain.RecommendationAggregate.MemberRecommendationPreference.Create(
                member.Id, enabled, updatedAt ?? _now));
        await Context.SaveChangesAsync();
    }

    public async Task<CheckoutPassageTestMember> AddMemberWithCardAsync(string firstName)
    {
        var userId = UserId.Create(Guid.NewGuid());
        var user = User.CreateFromExternalIdentity(
            userId,
            userId.Value.ToString("N"),
            $"member-{userId.Value:N}@example.test",
            _now,
            new Name(firstName, "Durand"));
        var recoveryCode = $"{MemberCardRecoveryCode.Words[Context.MemberCards.Count()]}-{(4271 + Context.MemberCards.Count()).ToString("D4")}";
        var card = MemberCard.Issue(Guid.NewGuid(), userId, recoveryCode, _now);
        Context.Users.Add(user);
        Context.MemberCards.Add(card);
        await Context.SaveChangesAsync();
        return new CheckoutPassageTestMember(user, _tokens.Create(card.Id, card.Version), recoveryCode);
    }

    public async Task RotateCardAsync(CheckoutPassageTestMember member)
    {
        var card = await Context.MemberCards.SingleAsync(candidate => candidate.UserId == member.User.Id);
        card.Rotate("SABLE-3159", _now.AddMinutes(1));
        await Context.SaveChangesAsync();
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

    public async Task RegisterSaleAsync(string isbnValue, Guid passageId, Guid? gestureId = null)
    {
        Book book;
        BookMovement movement;
        if (gestureId is { } gesture && _salesByGesture.TryGetValue(gesture, out var recorded))
        {
            (book, movement) = recorded;
        }
        else
        {
            (book, movement) = await AddSaleAsync(isbnValue, passageId, gestureId);
            if (gestureId is { } newGesture)
            {
                _salesByGesture[newGesture] = (book, movement);
            }
        }

        await CreateRecorder().RecordOrdinarySaleAsync(passageId, movement, book, isbnValue, default);
        await Context.SaveChangesAsync();
    }

    public async Task<(Book Book, BookMovement Movement)> AddSaleAsync(
        string isbnValue,
        Guid passageId,
        Guid? gestureId = null,
        DateTime? occurredAt = null)
    {
        var isbn = ParseIsbn(_redirects.GetValueOrDefault(isbnValue, isbnValue));
        var book = await Context.Books.SingleOrDefaultAsync(candidate => candidate.Id == isbn)
            ?? Book.Create(isbn, _now);
        if (!Context.Books.Local.Contains(book))
        {
            Context.Books.Add(book);
        }

        var movement = CreateMovement(isbn, passageId, gestureId, occurredAt ?? _now);
        Context.BookMovements.Add(movement);
        await Context.SaveChangesAsync();
        return (book, movement);
    }

    public async Task<CheckoutPassage> AddAssociatedPassageWithSaleAsync(User member, string requestedIsbn)
    {
        return await AddAssociatedPassageAsync(member, _now, requestedIsbn);
    }

    public async Task<CheckoutPassage> AddAssociatedPassageAsync(
        User member,
        DateTime occurredAt,
        params string[] isbns)
    {
        var passageId = Guid.NewGuid();
        var passage = CheckoutPassage.OpenFromSale(passageId, occurredAt, _now);
        passage.Associate(member.Id, VolunteerId, occurredAt);
        Context.CheckoutPassages.Add(passage);
        foreach (var requestedIsbn in isbns)
        {
            var (book, movement) = await AddSaleAsync(requestedIsbn, passageId, occurredAt: occurredAt);
            Context.CheckoutPassageLines.Add(CheckoutPassageLine.ForOrdinarySale(
                Guid.NewGuid(), passageId, movement, book, requestedIsbn));
        }

        await Context.SaveChangesAsync();
        return passage;
    }

    public async Task<CheckoutPassage> AddAssociatedPassageWithoutLinesAsync(User member, DateTime occurredAt)
    {
        var passage = CheckoutPassage.OpenFromSale(Guid.NewGuid(), occurredAt, _now);
        passage.Associate(member.Id, VolunteerId, occurredAt);
        Context.CheckoutPassages.Add(passage);
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

    private BookMovement CreateMovement(
        Isbn13 isbn,
        Guid passageId,
        Guid? gestureId = null,
        DateTime? occurredAt = null) => BookMovement.Create(
        BookMovementId.Create(Guid.NewGuid()),
        isbn,
        BookMovementType.Sale,
        -1,
        occurredAt ?? _now,
        _now,
        clockSuspect: false,
        scanSessionId: null,
        volunteerId: null,
        assoEventsId: null,
        note: null,
        clientGestureId: gestureId,
        checkoutPassageId: passageId);

    private static Isbn13 ParseIsbn(string value) => Isbn13.TryCreate(value, out var isbn)
        ? isbn
        : throw new InvalidOperationException($"Invalid test ISBN: {value}");
}

internal sealed class CheckoutPassageTestDbContext(DbContextOptions<CheckoutPassageTestDbContext> options)
    : DbContext(options), IProjectDbContext
{
    public bool ThrowWhenRecommendationDataIsRead { get; set; }

    public DbSet<User> Users => Set<User>();
    public DbSet<Book> Books => Set<Book>();
    public DbSet<MemberCard> MemberCards => Set<MemberCard>();
    public DbSet<BookMovement> BookMovements => Set<BookMovement>();
    public DbSet<MemberSelectionItem> MemberSelectionItems => Set<MemberSelectionItem>();
    public DbSet<CheckoutPassage> CheckoutPassages => Set<CheckoutPassage>();
    public DbSet<CheckoutPassageLine> CheckoutPassageLines => Set<CheckoutPassageLine>();
    public DbSet<BookAnnouncement> BookAnnouncements => Set<BookAnnouncement>();
    public DbSet<AssoEvents> AssoEvents => Set<AssoEvents>();
    public DbSet<Vole_Papillon_Damour.Domain.RecommendationAggregate.BookNeighbor> BookNeighbors =>
        Set<Vole_Papillon_Damour.Domain.RecommendationAggregate.BookNeighbor>();
    public DbSet<Vole_Papillon_Damour.Domain.RecommendationAggregate.RecommendationGeneration>
        RecommendationGenerations => Set<Vole_Papillon_Damour.Domain.RecommendationAggregate.RecommendationGeneration>();
    public DbSet<Vole_Papillon_Damour.Domain.RecommendationAggregate.MemberRecommendationPreference>
        MemberRecommendationPreferences => Set<Vole_Papillon_Damour.Domain.RecommendationAggregate.MemberRecommendationPreference>();

    DbSet<Product> IProjectDbContext.Products => throw new NotSupportedException();
    DbSet<AssoEvents> IProjectDbContext.AssoEvents => AssoEvents;
    DbSet<Order> IProjectDbContext.Orders => throw new NotSupportedException();
    DbSet<Book> IProjectDbContext.Books => Books;
    DbSet<BookAnnouncement> IProjectDbContext.BookAnnouncements => BookAnnouncements;
    DbSet<ScanSession> IProjectDbContext.ScanSessions => throw new NotSupportedException();
    DbSet<AssociationSettings> IProjectDbContext.AssociationSettings => throw new NotSupportedException();
    DbSet<Watchlist> IProjectDbContext.Watchlists => throw new NotSupportedException();
    DbSet<WatchlistItem> IProjectDbContext.WatchlistItems => throw new NotSupportedException();
    DbSet<UserAlertHistory> IProjectDbContext.UserAlertHistories => throw new NotSupportedException();
    DbSet<EmailBounceEvent> IProjectDbContext.EmailBounceEvents => throw new NotSupportedException();
    DbSet<RareBook> IProjectDbContext.RareBooks => throw new NotSupportedException();
    DbSet<RareBookPhoto> IProjectDbContext.RareBookPhotos => throw new NotSupportedException();
    DbSet<MemberCard> IProjectDbContext.MemberCards => MemberCards;
    DbSet<RareBookTombstone> IProjectDbContext.RareBookTombstones => throw new NotSupportedException();
    DbSet<Vole_Papillon_Damour.Domain.RecommendationAggregate.BookNeighbor>
        IProjectDbContext.BookNeighbors => ThrowWhenRecommendationDataIsRead
            ? throw new InvalidOperationException("Neighbor data must not be read while recommendations are disabled.")
            : BookNeighbors;
    DbSet<Vole_Papillon_Damour.Domain.RecommendationAggregate.RecommendationGeneration>
        IProjectDbContext.RecommendationGenerations => ThrowWhenRecommendationDataIsRead
            ? throw new InvalidOperationException("Generation data must not be read while recommendations are disabled.")
            : RecommendationGenerations;
    DbSet<Vole_Papillon_Damour.Domain.RecommendationAggregate.MemberRecommendationPreference>
        IProjectDbContext.MemberRecommendationPreferences => MemberRecommendationPreferences;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Ignore<Product>();
        modelBuilder.Ignore<AssoEvents>();
        modelBuilder.Ignore<Order>();
        modelBuilder.Ignore<BookAnnouncement>();
        modelBuilder.Ignore<ScanSession>();
        modelBuilder.Ignore<AssociationSettings>();
        modelBuilder.Ignore<Watchlist>();
        modelBuilder.Ignore<WatchlistItem>();
        modelBuilder.Ignore<UserAlertHistory>();
        modelBuilder.Ignore<EmailBounceEvent>();
        modelBuilder.Ignore<RareBook>();
        modelBuilder.Ignore<RareBookPhoto>();
        modelBuilder.ApplyConfiguration(new MemberCardConfiguration());
        modelBuilder.ApplyConfiguration(new BookConfiguration());
        modelBuilder.Entity<Book>().Property(book => book.RawPayload).HasColumnType("TEXT");
        modelBuilder.Entity<Book>().Property(book => book.ManuallyEditedFields).HasColumnType("TEXT");
        modelBuilder.Entity<Book>().Property(book => book.RowVersion)
            .ValueGeneratedNever()
            .IsConcurrencyToken(false);
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
        modelBuilder.ApplyConfiguration(new Vole_Papillon_Damour.Infrastructure.Persistence.Configurations.BookNeighborConfiguration());
        modelBuilder.ApplyConfiguration(new Vole_Papillon_Damour.Infrastructure.Persistence.Configurations.RecommendationGenerationConfiguration());
        modelBuilder.ApplyConfiguration(new Vole_Papillon_Damour.Infrastructure.Persistence.Configurations.MemberRecommendationPreferenceConfiguration());

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
    }

    private static Isbn13 ParseIsbn(string value) => Isbn13.TryCreate(value, out var isbn)
        ? isbn
        : throw new InvalidOperationException($"Invalid test ISBN: {value}");
}


internal sealed record CheckoutPassageTestMember(User User, string QrPayload, string RecoveryCode)
{
    public UserId UserId => User.Id;
}

internal sealed class CheckoutPassageTestClock(DateTime utcNow) : IDateTimeProvider
{
    public DateTime UtcNow { get; } = utcNow;
}
