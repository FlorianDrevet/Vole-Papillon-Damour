using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Vole_Papillon_Damour.Application.Common.Interfaces.Persistence;
using Vole_Papillon_Damour.Domain.BookAggregate;
using Vole_Papillon_Damour.Domain.BookAggregate.Entities;
using Vole_Papillon_Damour.Domain.BookAggregate.ValueObjects;
using Vole_Papillon_Damour.Domain.IsbndbAggregate;
using Vole_Papillon_Damour.Domain.UserAggregate.ValueObjects;
using Vole_Papillon_Damour.Infrastructure.Persistence;

namespace Vole_Papillon_Damour.Infrastructure.tests.Persistence;

public sealed class IsbndbPersistenceTests
{
    [Fact]
    public async Task SqliteRoundTrip_PersistsIsbndbBooksNoticesAndQuotaData()
    {
        await using var connection = await OpenConnectionAsync();
        await using var context = await CreateContextAsync(connection);

        var fetchedAt = new DateTime(2026, 10, 23, 10, 30, 0, DateTimeKind.Utc);
        var isbn13 = ParseIsbn13("9782070363735");
        var book = Book.Create(isbn13, fetchedAt.AddDays(-1));
        book.ApplyIsbndbNotice(
            new BookMetadataPatch(
                Title: "Le Petit Prince",
                Authors: "Antoine de Saint-Exupéry",
                Publisher: "Gallimard",
                PublicationYear: 1943,
                PhysicalFormat: "Broché",
                Language: "fr",
                Genre: null,
                CoverUrl: null,
                Fields:
                [
                    BookMetadataField.Title,
                    BookMetadataField.Authors,
                    BookMetadataField.Publisher,
                    BookMetadataField.PublicationYear,
                    BookMetadataField.Pages,
                    BookMetadataField.Edition
                ],
                Pages: 96,
                Edition: "Édition illustrée"),
            fetchedAt);

        var record = BookIsbndbRecord.Create(
            isbn13,
            fetchedAt,
            titleLong: "Le Petit Prince: édition illustrée",
            isbn10: "2070363736",
            publisher: "Gallimard",
            binding: "Paperback",
            datePublished: "1943-04-06",
            edition: "Illustrated",
            editionNumber: 4,
            dimensions: "20 x 13 x 1 cm",
            heightValue: 20,
            heightUnit: "centimeters",
            widthValue: 13,
            widthUnit: "centimeters",
            lengthValue: 1,
            lengthUnit: "centimeters",
            weightValue: 250,
            weightUnit: "grams",
            deweyDecimal: "843.912",
            synopsis: "Un aviateur rencontre un petit prince.",
            overview: "Une édition illustrée du conte.",
            excerpt: "S'il vous plaît… dessine-moi un mouton.",
            subjects: "Fiction;Classics",
            authors: "Antoine de Saint-Exupéry",
            otherIsbns: "9782070612758",
            related: "{\"workId\":\"prince\"}",
            msrp: 29.99m,
            listPriceAmount: 24.50m,
            listPriceCurrency: "EUR",
            languageCode: "fre",
            imageUrl: "https://example.test/cover.jpg",
            rawJson: "{\"title\":\"Le Petit Prince\"}");

        var absentIsbn = ParseIsbn13("9782070612758");
        var absentBook = Book.Create(absentIsbn, fetchedAt.AddDays(-2));
        absentBook.ApplyManualMetadata(
            new BookMetadataPatch(
                Title: "Livre hors ISBNdb",
                Authors: "Auteur de l'association",
                Publisher: null,
                PublicationYear: null,
                PhysicalFormat: null,
                Language: null,
                Genre: null,
                CoverUrl: null,
                Fields: [BookMetadataField.Title, BookMetadataField.Authors]),
            fetchedAt.AddMinutes(-5));
        absentBook.MarkAbsentFromIsbndb(fetchedAt.AddMinutes(-4));
        var completer = UserId.CreateUnique();
        absentBook.MarkIsbndbCompleted(completer, fetchedAt.AddMinutes(-3));

        var quotaState = await context.IsbndbQuotaStates.SingleAsync();
        quotaState.Observe(5_000, 4_321, fetchedAt.AddDays(1), fetchedAt);

        var dailyUsage = IsbndbDailyUsage.Create(DateOnly.FromDateTime(fetchedAt), IsbndbUsage.PublicSearch);
        dailyUsage.Increment(3);
        var catchUpRun = IsbndbCatchUpRun.Create(fetchedAt.AddHours(-1));
        catchUpRun.Finish(
            fetchedAt,
            filled: 12,
            absent: 4,
            rechecked: 5,
            remaining: 100,
            IsbndbCatchUpStopReason.QuotaExhausted);

        context.Books.AddRange(book, absentBook);
        context.BookIsbndbRecords.Add(record);
        context.IsbndbDailyUsages.Add(dailyUsage);
        context.IsbndbCatchUpRuns.Add(catchUpRun);
        await context.SaveChangesAsync();

        context.ChangeTracker.Clear();

        var persistedBook = await context.Books.SingleAsync(candidate => candidate.Id == isbn13);
        persistedBook.IsIsbndbSourced.Should().BeTrue();
        persistedBook.IsAbsentFromIsbndb.Should().BeFalse();
        persistedBook.IsbndbCheckedAt.Should().Be(fetchedAt);
        persistedBook.Pages.Should().Be(96);
        persistedBook.Edition.Should().Be("Édition illustrée");
        persistedBook.WorkKey.Should().Be(book.WorkKey);

        var persistedRecord = await context.BookIsbndbRecords.SingleAsync(candidate => candidate.Isbn13 == isbn13);
        persistedRecord.TitleLong.Should().Be("Le Petit Prince: édition illustrée");
        persistedRecord.Isbn10.Should().Be("2070363736");
        persistedRecord.HeightValue.Should().Be(20);
        persistedRecord.WeightUnit.Should().Be("grams");
        persistedRecord.OtherIsbns.Should().Be("9782070612758");
        persistedRecord.RawJson.Should().Be("{\"title\":\"Le Petit Prince\"}");
        persistedRecord.FetchedAt.Should().Be(fetchedAt);

        var persistedAbsentBook = await context.Books.SingleAsync(candidate => candidate.Id == absentIsbn);
        persistedAbsentBook.IsIsbndbSourced.Should().BeFalse();
        persistedAbsentBook.IsAbsentFromIsbndb.Should().BeTrue();
        persistedAbsentBook.IsbndbCompletedAt.Should().Be(fetchedAt.AddMinutes(-3));
        persistedAbsentBook.IsbndbCompletedBy!.Value.Should().Be(completer.Value);

        var persistedQuota = await context.IsbndbQuotaStates.SingleAsync();
        persistedQuota.Id.Should().Be(IsbndbQuotaState.SingletonId);
        persistedQuota.DailyLimit.Should().Be(5_000);
        persistedQuota.DailyRemaining.Should().Be(4_321);
        persistedQuota.ResetsAt.Should().Be(fetchedAt.AddDays(1));
        persistedQuota.ObservedAt.Should().Be(fetchedAt);

        var persistedUsage = await context.IsbndbDailyUsages.SingleAsync();
        persistedUsage.Day.Should().Be(DateOnly.FromDateTime(fetchedAt));
        persistedUsage.Usage.Should().Be(IsbndbUsage.PublicSearch);
        persistedUsage.Credits.Should().Be(3);

        var persistedRun = await context.IsbndbCatchUpRuns.SingleAsync();
        persistedRun.StartedAt.Should().Be(fetchedAt.AddHours(-1));
        persistedRun.EndedAt.Should().Be(fetchedAt);
        persistedRun.Filled.Should().Be(12);
        persistedRun.Absent.Should().Be(4);
        persistedRun.Rechecked.Should().Be(5);
        persistedRun.Remaining.Should().Be(100);
        persistedRun.StopReason.Should().Be(IsbndbCatchUpStopReason.QuotaExhausted);
    }

    [Fact]
    public void Model_MapsRequiredProvenanceDefaultsAndIndexes()
    {
        using var context = new ProjectDbContext(
            new DbContextOptionsBuilder<ProjectDbContext>()
                .UseSqlite("Data Source=:memory:")
                .Options);
        var book = context.Model.FindEntityType(typeof(Book))!;

        book.FindProperty(nameof(Book.IsIsbndbSourced))!.GetDefaultValue().Should().Be(false);
        book.FindProperty(nameof(Book.IsAbsentFromIsbndb))!.GetDefaultValue().Should().Be(false);
        book.FindProperty(nameof(Book.IsIsbndbSourced))!.IsNullable.Should().BeFalse();
        book.FindProperty(nameof(Book.IsAbsentFromIsbndb))!.IsNullable.Should().BeFalse();
        book.FindProperty(nameof(Book.Edition))!.GetMaxLength().Should().Be(100);
        book.FindProperty(nameof(Book.WorkKey))!.GetMaxLength().Should().Be(200);

        book.GetIndexes().Should().Contain(index =>
            index.Properties.Select(property => property.Name)
                .SequenceEqual(new[] { nameof(Book.WorkKey) }));
        book.GetIndexes().Should().Contain(index =>
            index.Properties.Select(property => property.Name)
                .SequenceEqual(new[] { nameof(Book.IsIsbndbSourced), nameof(Book.IsAbsentFromIsbndb) }) &&
            index.GetFilter() == "[IsIsbndbSourced] = 0");

        var record = context.Model.FindEntityType(typeof(BookIsbndbRecord))!;
        var foreignKey = record.GetForeignKeys().Should().ContainSingle().Subject;
        foreignKey.PrincipalEntityType.ClrType.Should().Be(typeof(Book));
        foreignKey.Properties.Select(property => property.Name)
            .Should().Equal(nameof(BookIsbndbRecord.Isbn13));
        foreignKey.DeleteBehavior.Should().Be(Microsoft.EntityFrameworkCore.DeleteBehavior.Cascade);
    }

    private static async Task<SqliteConnection> OpenConnectionAsync()
    {
        var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        connection.CreateCollation(
            "Latin1_General_100_CI_AI",
            (left, right) => string.Compare(left, right, StringComparison.OrdinalIgnoreCase));
        return connection;
    }

    private static async Task<ProjectDbContext> CreateContextAsync(SqliteConnection connection)
    {
        var options = new DbContextOptionsBuilder<ProjectDbContext>()
            .UseSqlite(connection)
            .Options;
        var context = new TestDbContext(options);
        await context.Database.EnsureCreatedAsync();
        return context;
    }

    private static Isbn13 ParseIsbn13(string value) =>
        Isbn13.TryCreate(value, out var isbn13)
            ? isbn13
            : throw new InvalidOperationException($"'{value}' is not a valid ISBN-13.");

    private sealed class TestDbContext(DbContextOptions<ProjectDbContext> options)
        : ProjectDbContext(options)
    {
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<Book>()
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
}
