using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using Vole_Papillon_Damour.Application.Common.Interfaces.Services;
using Vole_Papillon_Damour.Application.Common.Services;
using Vole_Papillon_Damour.Application.Favorites;
using Vole_Papillon_Damour.Application.Favorites.Common;
using Vole_Papillon_Damour.Application.Favorites.Commands.AddMyFavorite;
using Vole_Papillon_Damour.Application.Favorites.Commands.MergeMyFavorites;
using Vole_Papillon_Damour.Application.Favorites.Commands.RemoveMyFavorite;
using Vole_Papillon_Damour.Domain.BookAggregate.ValueObjects;
using Vole_Papillon_Damour.Domain.FavoriteBookAggregate;
using Vole_Papillon_Damour.Domain.IsbndbAggregate;
using Vole_Papillon_Damour.Domain.UserAggregate.ValueObjects;
using Vole_Papillon_Damour.Infrastructure.Persistence;

namespace Vole_Papillon_Damour.Application.tests.Favorites;

public sealed class MemberFavoriteCommandHandlerTests
{
    private static readonly DateTime Now = new(2026, 11, 14, 9, 0, 0, DateTimeKind.Utc);
    private static readonly Guid MemberId = Guid.Parse("d30abca1-05fa-4005-9e12-912a7485c31a");

    [Fact]
    public async Task AddFavorite_IsIdempotentForTheSameMemberAndIsbn()
    {
        await using var fixture = await Fixture.CreateAsync();
        var handler = CreateAddHandler(fixture.Context, accountMax: 200);
        var command = CommandFor("9782070612758");

        var added = await handler.Handle(command, CancellationToken.None);
        var repeated = await handler.Handle(command, CancellationToken.None);

        added.IsError.Should().BeFalse();
        added.Value.AlreadyPresent.Should().BeFalse();
        repeated.IsError.Should().BeFalse();
        repeated.Value.AlreadyPresent.Should().BeTrue();
        (await fixture.Context.MemberFavoriteBooks.CountAsync()).Should().Be(1);
        (await fixture.Context.Books.SingleAsync()).UpdatedAt.Should().BeAfter(Now);
    }

    [Fact]
    public async Task AddFavorite_RejectsTheTwoHundredAndFirstUniqueFavorite()
    {
        await using var fixture = await Fixture.CreateAsync();
        var handler = CreateAddHandler(fixture.Context, accountMax: 200);
        var memberIdentity = await CreateIdentity(fixture.Context).EnsureAsync(
            MemberId,
            "member@example.test",
            "Membre",
            null,
            CancellationToken.None);
        fixture.Context.MemberFavoriteBooks.AddRange(Enumerable.Range(0, 200)
            .Select(index => MemberFavoriteBook.Create(memberIdentity.Id, CreateIsbn(index), Now)));
        await fixture.Context.SaveChangesAsync();

        var result = await handler.Handle(CommandFor(CreateIsbn(200).Value), CancellationToken.None);

        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("Favorites.LimitReached");
        (await fixture.Context.MemberFavoriteBooks.CountAsync()).Should().Be(200);
    }

    [Fact]
    public async Task RemoveFavorite_IsIdempotentAndOnlyRemovesTheCurrentMemberEntry()
    {
        await using var fixture = await Fixture.CreateAsync();
        var handler = CreateRemoveHandler(fixture.Context);
        var isbn = ParseIsbn("9782070612758");
        var identity = await CreateIdentity(fixture.Context).EnsureAsync(
            MemberId,
            "member@example.test",
            "Membre",
            null,
            CancellationToken.None);
        fixture.Context.Books.Add(Vole_Papillon_Damour.Domain.BookAggregate.Book.Create(isbn, Now.AddDays(-2)));
        fixture.Context.MemberFavoriteBooks.Add(MemberFavoriteBook.Create(identity.Id, isbn, Now));
        await fixture.Context.SaveChangesAsync();

        var removed = await handler.Handle(RemoveCommandFor(isbn.Value), CancellationToken.None);
        var repeated = await handler.Handle(RemoveCommandFor(isbn.Value), CancellationToken.None);

        removed.IsError.Should().BeFalse();
        repeated.IsError.Should().BeFalse();
        (await fixture.Context.MemberFavoriteBooks.CountAsync()).Should().Be(0);
        (await fixture.Context.Books.SingleAsync()).UpdatedAt.Should().BeAfter(Now.AddDays(-2));
    }

    [Fact]
    public async Task MergeFavorites_AddsMissingEntriesAndDecrementsAnonymousCounts()
    {
        await using var fixture = await Fixture.CreateAsync();
        var identityService = CreateIdentity(fixture.Context);
        var member = await identityService.EnsureAsync(
            MemberId,
            "member@example.test",
            "Membre",
            null,
            CancellationToken.None);
        var existing = ParseIsbn("9782070612758");
        var firstNew = ParseIsbn("9782070409228");
        var secondNew = ParseIsbn("9782253004226");
        fixture.Context.MemberFavoriteBooks.Add(MemberFavoriteBook.Create(member.Id, existing, Now.AddDays(-2)));
        var anonymousCount = AnonymousFavoriteCount.Create(firstNew, Now.AddDays(-1));
        anonymousCount.Increment(Now.AddDays(-1));
        fixture.Context.AnonymousFavoriteCounts.Add(anonymousCount);
        await fixture.Context.SaveChangesAsync();
        var handler = CreateMergeHandler(fixture.Context, identityService, accountMax: 200);

        var result = await handler.Handle(
            MergeCommandFor(
            [
                new MergeMyFavoritesEntry(existing.Value, Now.AddDays(-2)),
                new MergeMyFavoritesEntry(firstNew.Value, Now.AddDays(-1)),
                new MergeMyFavoritesEntry(secondNew.Value, Now),
            ]),
            CancellationToken.None);

        result.IsError.Should().BeFalse();
        result.Value.Added.Should().Be(2);
        result.Value.Skipped.Should().Be(0);
        result.Value.Total.Should().Be(3);
        (await fixture.Context.MemberFavoriteBooks.CountAsync()).Should().Be(3);
        (await fixture.Context.AnonymousFavoriteCounts.SingleAsync()).Count.Should().Be(0);
        (await fixture.Context.Books.Where(book => new[] { firstNew, secondNew }.Contains(book.Id))
            .AllAsync(book => book.UpdatedAt > Now)).Should().BeTrue();
    }

    [Fact]
    public async Task MergeFavorites_WhenCapacityIsInsufficientSkipsTheOldestLocalEntries()
    {
        await using var fixture = await Fixture.CreateAsync();
        var identityService = CreateIdentity(fixture.Context);
        var member = await identityService.EnsureAsync(
            MemberId,
            "member@example.test",
            "Membre",
            null,
            CancellationToken.None);
        var existing = ParseIsbn("9782070612758");
        fixture.Context.MemberFavoriteBooks.Add(MemberFavoriteBook.Create(member.Id, existing, Now.AddDays(-3)));
        await fixture.Context.SaveChangesAsync();
        var entries = Enumerable.Range(300, 3)
            .Select(index => new MergeMyFavoritesEntry(CreateIsbn(index).Value, Now.AddDays(index - 300)))
            .ToArray();
        var handler = CreateMergeHandler(fixture.Context, identityService, accountMax: 2);

        var result = await handler.Handle(MergeCommandFor(entries), CancellationToken.None);

        result.IsError.Should().BeFalse();
        result.Value.Added.Should().Be(1);
        result.Value.Skipped.Should().Be(2);
        result.Value.Total.Should().Be(2);
        (await fixture.Context.MemberFavoriteBooks.AnyAsync(item => item.Isbn13 == CreateIsbn(302))).Should().BeTrue();
    }

    private static AddMyFavoriteCommandHandler CreateAddHandler(TestDbContext context, int accountMax)
    {
        var identity = CreateIdentity(context);
        var (references, _, _) = CreateReferenceService(context);
        return new AddMyFavoriteCommandHandler(
            context,
            identity,
            references,
            CreateClock(),
            Microsoft.Extensions.Options.Options.Create(new FavoritesOptions { AccountMax = accountMax }));
    }

    private static RemoveMyFavoriteCommandHandler CreateRemoveHandler(TestDbContext context) =>
        new(context, CreateIdentity(context), CreateClock());

    private static MergeMyFavoritesCommandHandler CreateMergeHandler(
        TestDbContext context,
        MemberIdentityService identity,
        int accountMax)
    {
        var (references, _, _) = CreateReferenceService(context);
        return new MergeMyFavoritesCommandHandler(
            context,
            identity,
            references,
            CreateClock(),
            Microsoft.Extensions.Options.Options.Create(new FavoritesOptions { AccountMax = accountMax }));
    }

    private static (FavoriteReferenceService Service, IBookMetadataEnrichmentQueue EnrichmentQueue,
        IBookRecommendationWorkQueue Recommendations) CreateReferenceService(TestDbContext context)
    {
        var enrichmentQueue = Substitute.For<IBookMetadataEnrichmentQueue>();
        var recommendations = Substitute.For<IBookRecommendationWorkQueue>();
        var quotaGuard = Substitute.For<IIsbndbQuotaGuard>();
        quotaGuard.IsExhaustedAsync(Now, Arg.Any<CancellationToken>()).Returns(false);
        return (
            new FavoriteReferenceService(
                context,
                enrichmentQueue,
                recommendations,
                quotaGuard,
                CreateClock(),
                Microsoft.Extensions.Options.Options.Create(new FavoritesOptions())),
            enrichmentQueue,
            recommendations);
    }

    private static MemberIdentityService CreateIdentity(TestDbContext context) =>
        new(context, CreateClock());

    private static IDateTimeProvider CreateClock()
    {
        var clock = Substitute.For<IDateTimeProvider>();
        clock.UtcNow.Returns(Now);
        return clock;
    }

    private static AddMyFavoriteCommand CommandFor(string isbn13) =>
        new(MemberId, "member@example.test", "Membre", null, isbn13);

    private static RemoveMyFavoriteCommand RemoveCommandFor(string isbn13) =>
        new(MemberId, "member@example.test", "Membre", null, isbn13);

    private static MergeMyFavoritesCommand MergeCommandFor(IReadOnlyList<MergeMyFavoritesEntry> entries) =>
        new(MemberId, "member@example.test", "Membre", null, entries);

    private static Isbn13 CreateIsbn(int serial)
    {
        var firstTwelve = $"978{serial:D9}";
        var weightedSum = firstTwelve.Select((digit, index) =>
            (digit - '0') * (index % 2 == 0 ? 1 : 3)).Sum();
        return ParseIsbn($"{firstTwelve}{(10 - weightedSum % 10) % 10}");
    }

    private static Isbn13 ParseIsbn(string value) => Isbn13.TryCreate(value, out var isbn)
        ? isbn
        : throw new InvalidOperationException($"Invalid test ISBN: {value}");

    private sealed class Fixture : IAsyncDisposable
    {
        private readonly SqliteConnection connection;

        private Fixture(SqliteConnection connection, TestDbContext context)
        {
            this.connection = connection;
            Context = context;
        }

        public TestDbContext Context { get; }

        public static async Task<Fixture> CreateAsync()
        {
            var connection = new SqliteConnection("Data Source=:memory:");
            await connection.OpenAsync();
            connection.CreateCollation(
                "Latin1_General_100_CI_AI",
                (left, right) => string.Compare(left, right, StringComparison.OrdinalIgnoreCase));
            var options = new DbContextOptionsBuilder<ProjectDbContext>()
                .UseSqlite(connection)
                .Options;
            var context = new TestDbContext(options);
            await context.Database.EnsureCreatedAsync();
            return new Fixture(connection, context);
        }

        public async ValueTask DisposeAsync()
        {
            await Context.DisposeAsync();
            await connection.DisposeAsync();
        }
    }

    private sealed class TestDbContext(DbContextOptions<ProjectDbContext> options) : ProjectDbContext(options)
    {
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
            modelBuilder.Entity<Vole_Papillon_Damour.Domain.BookAggregate.Book>()
                .Property(book => book.RowVersion)
                .ValueGeneratedNever()
                .IsConcurrencyToken(false);
            foreach (var property in modelBuilder.Model.GetEntityTypes()
                         .SelectMany(entityType => entityType.GetProperties())
                         .Where(property => property.GetColumnType() == "nvarchar(max)"))
            {
                property.SetColumnType("TEXT");
            }
        }
    }
}
