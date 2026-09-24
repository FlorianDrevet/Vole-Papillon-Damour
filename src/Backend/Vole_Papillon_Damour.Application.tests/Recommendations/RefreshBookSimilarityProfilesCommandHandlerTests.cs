using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using NSubstitute;
using Vole_Papillon_Damour.Application.Common.Interfaces.Persistence;
using Vole_Papillon_Damour.Application.Common.Interfaces.Services;
using Vole_Papillon_Damour.Application.Recommendations.Commands.RefreshBookSimilarityProfiles;
using Vole_Papillon_Damour.Application.Recommendations.Similarity;
using Vole_Papillon_Damour.Domain.AssoEventsAggregate;
using Vole_Papillon_Damour.Domain.AssociationSettingsAggregate;
using Vole_Papillon_Damour.Domain.BookAggregate;
using Vole_Papillon_Damour.Domain.BookAggregate.Entities;
using Vole_Papillon_Damour.Domain.BookAggregate.ValueObjects;
using Vole_Papillon_Damour.Domain.BookMovementAggregate;
using Vole_Papillon_Damour.Domain.CheckoutPassageAggregate;
using Vole_Papillon_Damour.Domain.MemberCardAggregate;
using Vole_Papillon_Damour.Domain.MemberSelectionAggregate;
using Vole_Papillon_Damour.Domain.OrderAggregate;
using Vole_Papillon_Damour.Domain.ProductAggregate;
using Vole_Papillon_Damour.Domain.RareBookAggregate;
using Vole_Papillon_Damour.Domain.RareBookAggregate.Entities;
using Vole_Papillon_Damour.Domain.RecommendationAggregate;
using Vole_Papillon_Damour.Domain.ScanSessionAggregate;
using Vole_Papillon_Damour.Domain.UserAggregate;
using Vole_Papillon_Damour.Domain.WatchlistAggregate;
using Vole_Papillon_Damour.Infrastructure.Persistence.Configurations;

namespace Vole_Papillon_Damour.Application.tests.Recommendations;

public sealed class RefreshBookSimilarityProfilesCommandHandlerTests
{
    private static readonly DateTime Now = new(2026, 9, 25, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task Handle_ReadsOnlyVisibleCanonicalBooksWithoutFreshProfiles()
    {
        await using var fixture = await RecommendationProfileFixture.CreateAsync();
        await fixture.AddBookAsync("9782070363735", Now.AddDays(-20));
        await fixture.AddBookAsync("9783140464079", Now.AddDays(-19));
        await fixture.AddBookAsync("9782266233200", Now.AddDays(-18), hidden: true);
        await fixture.AddBookAsync("9782266282000", Now.AddDays(-30));
        await fixture.AddBookAsync("9782221249420", Now.AddDays(-17), redirectTo: "9782266282000");
        await fixture.AddProfileAsync("9782266282000", Now.AddDays(-10));
        var reader = new CapturingNoticeReader(_ => Edition("9782070363735"));
        var handler = CreateHandler(fixture.Context, reader, enabled: true);

        var result = await handler.Handle(new RefreshBookSimilarityProfilesCommand(), CancellationToken.None);

        result.Should().Be(new RefreshBookSimilarityProfilesResult(2, 2, 0));
        reader.Calls.Should().Equal("9782070363735", "9783140464079");
        fixture.Context.BookSimilarityProfiles.Select(profile => profile.Isbn13)
            .Should().Contain(reader.Calls);
    }

    [Fact]
    public async Task Handle_RefreshesProfilesOlderThanNinetyDaysOrTheirBook()
    {
        await using var fixture = await RecommendationProfileFixture.CreateAsync();
        await fixture.AddBookAsync("9782070363735", Now.AddDays(-20));
        await fixture.AddProfileAsync("9782070363735", Now.AddDays(-10));
        await fixture.AddBookAsync("9783140464079", Now.AddDays(-92));
        await fixture.AddProfileAsync("9783140464079", Now.AddDays(-91));
        await fixture.AddBookAsync("9782253157533", Now.AddDays(-1));
        await fixture.AddProfileAsync("9782253157533", Now.AddDays(-5));
        var reader = new CapturingNoticeReader(_ => Edition("9783140464079"));
        var handler = CreateHandler(fixture.Context, reader, enabled: true);

        var result = await handler.Handle(new RefreshBookSimilarityProfilesCommand(), CancellationToken.None);

        result.Should().Be(new RefreshBookSimilarityProfilesResult(2, 2, 0));
        reader.Calls.Should().Equal("9783140464079", "9782253157533");
    }

    [Fact]
    public async Task Handle_StoresNotFoundNoticeSoItIsNotRetriedImmediately()
    {
        await using var fixture = await RecommendationProfileFixture.CreateAsync();
        await fixture.AddBookAsync("9782070363735", Now.AddDays(-1));
        var reader = new CapturingNoticeReader(_ => null);
        var handler = CreateHandler(fixture.Context, reader, enabled: true);

        var result = await handler.Handle(new RefreshBookSimilarityProfilesCommand(), CancellationToken.None);

        result.Should().Be(new RefreshBookSimilarityProfilesResult(1, 0, 1));
        var profile = await fixture.Context.BookSimilarityProfiles.SingleAsync();
        profile.NoticeFound.Should().BeFalse();
        profile.NoticeJson.Should().BeNull();
        profile.NoticeFetchedAt.Should().Be(Now);
    }

    [Fact]
    public async Task Handle_WhenDisabled_DoesNotReadOrWriteProfiles()
    {
        await using var fixture = await RecommendationProfileFixture.CreateAsync();
        await fixture.AddBookAsync("9782070363735", Now.AddDays(-1));
        var reader = new CapturingNoticeReader(_ => Edition("9782070363735"));
        var handler = CreateHandler(fixture.Context, reader, enabled: false);

        var result = await handler.Handle(new RefreshBookSimilarityProfilesCommand(), CancellationToken.None);

        result.Should().Be(new RefreshBookSimilarityProfilesResult(0, 0, 0));
        reader.Calls.Should().BeEmpty();
        fixture.Context.BookSimilarityProfiles.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_ProcessesOldestCandidatesUpToConfiguredBatchSize()
    {
        await using var fixture = await RecommendationProfileFixture.CreateAsync();
        await fixture.AddBookAsync("9782070363735", Now.AddDays(-2));
        await fixture.AddBookAsync("9783140464079", Now.AddDays(-20));
        var reader = new CapturingNoticeReader(_ => Edition("9782070363735"));
        var handler = CreateHandler(fixture.Context, reader, enabled: true, profileBatchSize: 1);

        var result = await handler.Handle(new RefreshBookSimilarityProfilesCommand(), CancellationToken.None);

        result.Should().Be(new RefreshBookSimilarityProfilesResult(1, 1, 0));
        reader.Calls.Should().Equal("9783140464079");
    }

    private static RefreshBookSimilarityProfilesCommandHandler CreateHandler(
        RecommendationProfileTestDbContext context,
        IBibliographicNoticeReader reader,
        bool enabled,
        int profileBatchSize = 200)
    {
        var settings = Substitute.For<IRecommendationSettings>();
        settings.Enabled.Returns(enabled);
        settings.ProfileBatchSize.Returns(profileBatchSize);
        var clock = Substitute.For<IDateTimeProvider>();
        clock.UtcNow.Returns(Now);
        return new RefreshBookSimilarityProfilesCommandHandler(context, reader, settings, clock);
    }

    private static SimilarityEdition Edition(string isbn13) => new(
        isbn13,
        "Titre de test",
        null,
        null,
        null,
        ["Auteur"],
        ["Auteur"],
        null,
        null,
        null,
        null,
        null,
        null,
        [],
        [],
        null,
        false,
        null,
        null,
        null);
}

internal sealed class RecommendationProfileFixture : IAsyncDisposable
{
    private readonly SqliteConnection _connection;

    private RecommendationProfileFixture(SqliteConnection connection, RecommendationProfileTestDbContext context)
    {
        _connection = connection;
        Context = context;
    }

    public RecommendationProfileTestDbContext Context { get; }

    public static async Task<RecommendationProfileFixture> CreateAsync()
    {
        var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        connection.CreateCollation(
            "Latin1_General_100_CI_AI",
            (left, right) => string.Compare(left, right, StringComparison.OrdinalIgnoreCase));
        var options = new DbContextOptionsBuilder<RecommendationProfileTestDbContext>()
            .UseSqlite(connection)
            .Options;
        var context = new RecommendationProfileTestDbContext(options);
        await context.Database.EnsureCreatedAsync();
        return new RecommendationProfileFixture(connection, context);
    }

    public async Task AddBookAsync(string isbn13, DateTime updatedAt, bool hidden = false, string? redirectTo = null)
    {
        var book = Book.Create(ParseIsbn(isbn13), updatedAt);
        if (hidden)
        {
            book.UpdateCatalogVisibility(true, updatedAt);
        }

        if (redirectTo is not null)
        {
            book.RedirectTo(ParseIsbn(redirectTo));
        }

        Context.Books.Add(book);
        await Context.SaveChangesAsync();
    }

    public async Task AddProfileAsync(string isbn13, DateTime fetchedAt)
    {
        var profile = BookSimilarityProfile.Create(isbn13);
        profile.RecordNotice("{}", true, fetchedAt);
        Context.BookSimilarityProfiles.Add(profile);
        await Context.SaveChangesAsync();
    }

    public async ValueTask DisposeAsync()
    {
        await Context.DisposeAsync();
        await _connection.DisposeAsync();
    }

    private static Isbn13 ParseIsbn(string value) => Isbn13.TryCreate(value, out var isbn)
        ? isbn
        : throw new InvalidOperationException($"Invalid test ISBN: {value}");
}

internal sealed class CapturingNoticeReader(Func<string, SimilarityEdition?> read) : IBibliographicNoticeReader
{
    public List<string> Calls { get; } = [];

    public Task<SimilarityEdition?> ReadAsync(string isbn13, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Calls.Add(isbn13);
        return Task.FromResult(read(isbn13));
    }
}

internal sealed class RecommendationProfileTestDbContext(DbContextOptions<RecommendationProfileTestDbContext> options)
    : DbContext(options), IProjectDbContext
{
    public DbSet<Book> Books => Set<Book>();
    public DbSet<BookSimilarityProfile> BookSimilarityProfiles => Set<BookSimilarityProfile>();

    DbSet<Product> IProjectDbContext.Products => throw new NotSupportedException();
    DbSet<User> IProjectDbContext.Users => throw new NotSupportedException();
    DbSet<AssoEvents> IProjectDbContext.AssoEvents => throw new NotSupportedException();
    DbSet<Order> IProjectDbContext.Orders => throw new NotSupportedException();
    DbSet<Book> IProjectDbContext.Books => Books;
    DbSet<BookAnnouncement> IProjectDbContext.BookAnnouncements => throw new NotSupportedException();
    DbSet<BookMovement> IProjectDbContext.BookMovements => throw new NotSupportedException();
    DbSet<ScanSession> IProjectDbContext.ScanSessions => throw new NotSupportedException();
    DbSet<AssociationSettings> IProjectDbContext.AssociationSettings => throw new NotSupportedException();
    DbSet<Watchlist> IProjectDbContext.Watchlists => throw new NotSupportedException();
    DbSet<WatchlistItem> IProjectDbContext.WatchlistItems => throw new NotSupportedException();
    DbSet<UserAlertHistory> IProjectDbContext.UserAlertHistories => throw new NotSupportedException();
    DbSet<EmailBounceEvent> IProjectDbContext.EmailBounceEvents => throw new NotSupportedException();
    DbSet<RareBook> IProjectDbContext.RareBooks => throw new NotSupportedException();
    DbSet<RareBookPhoto> IProjectDbContext.RareBookPhotos => throw new NotSupportedException();
    DbSet<MemberSelectionItem> IProjectDbContext.MemberSelectionItems => throw new NotSupportedException();
    DbSet<MemberCard> IProjectDbContext.MemberCards => throw new NotSupportedException();
    DbSet<CheckoutPassage> IProjectDbContext.CheckoutPassages => throw new NotSupportedException();
    DbSet<CheckoutPassageLine> IProjectDbContext.CheckoutPassageLines => throw new NotSupportedException();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Ignore<Product>();
        modelBuilder.Ignore<AssoEvents>();
        modelBuilder.Ignore<Order>();
        modelBuilder.Ignore<BookAnnouncement>();
        modelBuilder.Ignore<BookMovement>();
        modelBuilder.Ignore<CheckoutPassage>();
        modelBuilder.Ignore<CheckoutPassageLine>();
        modelBuilder.Ignore<MemberSelectionItem>();
        modelBuilder.Ignore<MemberCard>();
        modelBuilder.Ignore<ScanSession>();
        modelBuilder.Ignore<AssociationSettings>();
        modelBuilder.Ignore<Watchlist>();
        modelBuilder.Ignore<WatchlistItem>();
        modelBuilder.Ignore<UserAlertHistory>();
        modelBuilder.Ignore<EmailBounceEvent>();
        modelBuilder.Ignore<RareBook>();
        modelBuilder.Ignore<RareBookPhoto>();
        modelBuilder.Ignore<RareBookTombstone>();
        modelBuilder.Ignore<User>();
        modelBuilder.ApplyConfiguration(new BookConfiguration());
        modelBuilder.Entity<Book>().Property(book => book.RawPayload).HasColumnType("TEXT");
        modelBuilder.Entity<Book>().Property(book => book.ManuallyEditedFields).HasColumnType("TEXT");
        modelBuilder.Entity<Book>().Property(book => book.RowVersion)
            .ValueGeneratedNever()
            .IsConcurrencyToken(false);
        modelBuilder.ApplyConfiguration(new BookSimilarityProfileConfiguration());
        modelBuilder.Entity<BookSimilarityProfile>().Property(profile => profile.NoticeJson).HasColumnType("TEXT");
        modelBuilder.Entity<BookSimilarityProfile>().Property(profile => profile.ProfileText).HasColumnType("TEXT");
    }

    private static Isbn13 ParseIsbn(string value) => Isbn13.TryCreate(value, out var isbn)
        ? isbn
        : throw new InvalidOperationException($"Invalid stored ISBN: {value}");
}
