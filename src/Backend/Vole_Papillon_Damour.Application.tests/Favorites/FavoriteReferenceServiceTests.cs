using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.Options;
using NSubstitute;
using Vole_Papillon_Damour.Application.Common.Interfaces.Services;
using Vole_Papillon_Damour.Application.Favorites;
using Vole_Papillon_Damour.Application.Favorites.Common;
using Vole_Papillon_Damour.Application.Favorites.Queries.GetMyFavorites;
using Vole_Papillon_Damour.Application.Common.Services;
using Vole_Papillon_Damour.Application.Books.Common;
using Vole_Papillon_Damour.Domain.BookAggregate;
using Vole_Papillon_Damour.Domain.FavoriteBookAggregate;
using Vole_Papillon_Damour.Domain.BookAggregate.ValueObjects;
using Vole_Papillon_Damour.Domain.IsbndbAggregate;
using Vole_Papillon_Damour.Domain.RecommendationAggregate;
using Vole_Papillon_Damour.Domain.UserAggregate.ValueObjects;
using Vole_Papillon_Damour.Infrastructure.Persistence;
using Vole_Papillon_Damour.Infrastructure.Services.Bibliographic;

namespace Vole_Papillon_Damour.Application.tests.Favorites;

public sealed class FavoriteReferenceServiceTests
{
    private static readonly DateTime Now = new(2026, 11, 14, 9, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task EnsureAsync_ExistingBookDoesNotCreateOrQueueEnrichment()
    {
        await using var fixture = await Fixture.CreateAsync();
        var isbn = ParseIsbn("9782070612758");
        fixture.Context.Books.Add(Book.Create(isbn, Now));
        await fixture.Context.SaveChangesAsync();
        var queue = Substitute.For<IBookMetadataEnrichmentQueue>();
        var recommendations = Substitute.For<IBookRecommendationWorkQueue>();
        var service = CreateService(fixture.Context, queue, recommendations);

        var result = await service.EnsureAsync(isbn, FavoriteOrigin.Anonymous, CancellationToken.None);

        result.IsError.Should().BeFalse();
        result.Value.CanonicalIsbn13.Should().Be(isbn);
        result.Value.Created.Should().BeFalse();
        queue.DidNotReceiveWithAnyArgs().Enqueue(default);
        await recommendations.DidNotReceiveWithAnyArgs().EnqueueBooksAsync(default!, default);
        (await fixture.Context.Books.CountAsync()).Should().Be(1);
    }

    [Fact]
    public async Task EnsureAsync_RedirectedBookReturnsItsCanonicalNoticeWithoutCreatingAReference()
    {
        await using var fixture = await Fixture.CreateAsync();
        var requestedIsbn = ParseIsbn("9782070612758");
        var canonicalIsbn = ParseIsbn("9782070409228");
        var redirectedBook = Book.Create(requestedIsbn, Now.AddDays(-2));
        redirectedBook.RedirectTo(canonicalIsbn);
        fixture.Context.Books.AddRange(redirectedBook, Book.Create(canonicalIsbn, Now.AddDays(-1)));
        await fixture.Context.SaveChangesAsync();
        var queue = Substitute.For<IBookMetadataEnrichmentQueue>();
        var recommendations = Substitute.For<IBookRecommendationWorkQueue>();
        var service = CreateService(fixture.Context, queue, recommendations);

        var result = await service.EnsureAsync(requestedIsbn, FavoriteOrigin.Member, CancellationToken.None);

        result.IsError.Should().BeFalse();
        result.Value.CanonicalIsbn13.Should().Be(canonicalIsbn);
        result.Value.Created.Should().BeFalse();
        (await fixture.Context.Books.CountAsync()).Should().Be(2);
        queue.DidNotReceiveWithAnyArgs().Enqueue(default);
    }

    [Fact]
    public async Task EnsureAsync_UnknownIsbnCreatesReferenceAndQueuesFavoriteEnrichmentAndRecommendation()
    {
        await using var fixture = await Fixture.CreateAsync();
        var isbn = ParseIsbn("9782070612758");
        var queue = Substitute.For<IBookMetadataEnrichmentQueue>();
        var recommendations = Substitute.For<IBookRecommendationWorkQueue>();
        var service = CreateService(fixture.Context, queue, recommendations);

        var result = await service.EnsureAsync(isbn, FavoriteOrigin.Anonymous, CancellationToken.None);

        result.IsError.Should().BeFalse();
        result.Value.CanonicalIsbn13.Should().Be(isbn);
        result.Value.Created.Should().BeTrue();
        var book = await fixture.Context.Books.SingleAsync();
        book.IsFavoriteReference.Should().BeTrue();
        queue.Received(1).Enqueue(isbn, IsbndbUsage.Favorites, false);
        var job = await fixture.Context.BookRecommendationJobs.SingleAsync();
        job.Isbn13.Should().Be(isbn.Value);
        job.RequestVersion.Should().Be(1);
        await recommendations.Received(1).EnqueueBooksAsync(
            Arg.Is<IReadOnlyCollection<BookRecommendationQueueItem>>(items =>
                items.Count == 1 && items.Single().Isbn13 == isbn.Value && items.Single().RequestVersion == 1),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task EnsureAsync_ExhaustedFavoritesCreditsQueuesFallbackWithoutIsbndb()
    {
        await using var fixture = await Fixture.CreateAsync();
        var isbn = ParseIsbn("9782070612758");
        var dailyUsage = IsbndbDailyUsage.Create(DateOnly.FromDateTime(Now), IsbndbUsage.Favorites);
        dailyUsage.Increment(2);
        fixture.Context.IsbndbDailyUsages.Add(dailyUsage);
        await fixture.Context.SaveChangesAsync();
        var queue = Substitute.For<IBookMetadataEnrichmentQueue>();
        var recommendations = Substitute.For<IBookRecommendationWorkQueue>();
        var service = CreateService(
            fixture.Context,
            queue,
            recommendations,
            favoritesCredits: 2);

        var result = await service.EnsureAsync(isbn, FavoriteOrigin.Member, CancellationToken.None);

        result.IsError.Should().BeFalse();
        queue.Received(1).Enqueue(isbn, IsbndbUsage.Favorites, true);
    }

    [Fact]
    public async Task GetMyFavorites_GroupsFavoriteEditionsByWorkIdAndProjectsAvailability()
    {
        await using var fixture = await Fixture.CreateAsync();
        var clock = Substitute.For<IDateTimeProvider>();
        clock.UtcNow.Returns(Now);
        var user = await new MemberIdentityService(fixture.Context, clock).EnsureAsync(
            Guid.Parse("d30abca1-05fa-4005-9e12-912a7485c31a"),
            "member@example.test",
            null,
            null,
            CancellationToken.None);
        var firstIsbn = ParseIsbn("9782070612758");
        var secondIsbn = ParseIsbn("9782070409228");
        var firstBook = CreateResolvedBook(firstIsbn, "Titre récent", "work-42");
        firstBook.RecordAvailableEntry(Now.AddDays(-1), quantity: 2);
        var secondBook = CreateResolvedBook(secondIsbn, "Titre ancien", "work-42");
        fixture.Context.Books.AddRange(firstBook, secondBook);
        fixture.Context.MemberFavoriteBooks.AddRange(
            MemberFavoriteBook.Create(user.Id, firstIsbn, Now),
            MemberFavoriteBook.Create(user.Id, secondIsbn, Now.AddDays(-1)));
        await fixture.Context.SaveChangesAsync();

        var handler = new GetMyFavoritesQueryHandler(
            fixture.Context,
            new MemberIdentityService(fixture.Context, clock),
            clock,
            Options.Create(new FavoritesOptions { AccountMax = 200 }));
        var result = await handler.Handle(
            new GetMyFavoritesQuery(
                user.Id.Value,
                "member@example.test",
                null,
                null),
            CancellationToken.None);

        result.IsError.Should().BeFalse();
        result.Value.Total.Should().Be(2);
        result.Value.Maximum.Should().Be(200);
        result.Value.Works.Should().ContainSingle();
        var work = result.Value.Works.Single();
        work.WorkId.Should().Be("work-42");
        work.EditionCount.Should().Be(2);
        work.Editions.Select(edition => edition.Isbn13)
            .Should().Equal(firstIsbn.Value, secondIsbn.Value);
        work.Editions[0].QuantityAvailable.Should().Be(2);
        work.IsComputingSuggestions.Should().BeTrue();
    }

    private static FavoriteReferenceService CreateService(
        TestDbContext context,
        IBookMetadataEnrichmentQueue queue,
        IBookRecommendationWorkQueue recommendations,
        int favoritesCredits = 300)
    {
        var clock = Substitute.For<IDateTimeProvider>();
        clock.UtcNow.Returns(Now);
        var quotaGuard = Substitute.For<IIsbndbQuotaGuard>();
        quotaGuard.IsExhaustedAsync(Now, Arg.Any<CancellationToken>()).Returns(false);
        return new FavoriteReferenceService(
            context,
            queue,
            recommendations,
            quotaGuard,
            clock,
            Options.Create(new FavoritesOptions { ReferenceDailyIsbndbCredits = favoritesCredits }));
    }

    private static Isbn13 ParseIsbn(string value) => Isbn13.TryCreate(value, out var isbn)
        ? isbn
        : throw new InvalidOperationException($"Invalid test ISBN: {value}");

    private static Book CreateResolvedBook(Isbn13 isbn13, string title, string workId)
    {
        var book = Book.Create(isbn13, Now.AddDays(-10));
        book.ApplyAutomaticMetadata(
            new BookMetadataPatch(
                title,
                "Auteur",
                "Éditeur",
                2020,
                "Poche",
                "fr",
                "Roman",
                null,
                [
                    BookMetadataField.Title,
                    BookMetadataField.Authors,
                    BookMetadataField.Publisher,
                    BookMetadataField.PublicationYear,
                    BookMetadataField.PhysicalFormat,
                    BookMetadataField.Language,
                    BookMetadataField.Genre,
                    BookMetadataField.WorkId
                ],
                workId),
            BookMetadataSource.Bnf,
            Now.AddDays(-9),
            "{}");
        return book;
    }

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
            modelBuilder.Entity<Book>()
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
