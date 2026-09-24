using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Vole_Papillon_Damour.Domain.BookAggregate.ValueObjects;
using Vole_Papillon_Damour.Domain.NotFoundReportAggregate;
using Vole_Papillon_Damour.Domain.UserAggregate;
using Vole_Papillon_Damour.Domain.UserAggregate.ValueObjects;
using Vole_Papillon_Damour.Infrastructure.Persistence;

namespace Vole_Papillon_Damour.Infrastructure.tests.NotFoundReport;

public sealed class BookNotFoundReportConfigurationTests
{
    private static readonly DateTime Now = new(2026, 9, 24, 10, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task Configuration_MapsTableAndMaxLengths()
    {
        await using var connection = await OpenConnectionAsync();
        await using var context = await CreateContextAsync(connection);
        var entityType = context.Model.FindEntityType(typeof(BookNotFoundReport));

        entityType.Should().NotBeNull();
        entityType!.GetTableName().Should().Be("BookNotFoundReports");
        entityType.FindProperty(nameof(BookNotFoundReport.Comment))!.GetMaxLength().Should().Be(280);
        entityType.FindProperty(nameof(BookNotFoundReport.ClosureNote))!.GetMaxLength().Should().Be(500);
        entityType.FindProperty(nameof(BookNotFoundReport.Isbn13))!.GetColumnType().Should().Be("char(13)");
    }

    [Fact]
    public async Task Configuration_DeclaresFilteredUniqueIndexes()
    {
        await using var connection = await OpenConnectionAsync();
        await using var context = await CreateContextAsync(connection);
        var entityType = context.Model.FindEntityType(typeof(BookNotFoundReport));

        entityType.Should().NotBeNull();
        var indexes = entityType!.GetIndexes().ToDictionary(index => index.GetDatabaseName());

        indexes["UX_BookNotFoundReports_OpenPerMemberEdition"].IsUnique.Should().BeTrue();
        indexes["UX_BookNotFoundReports_OpenPerMemberEdition"].GetFilter()
            .Should().Be("[Status] = 0 AND [Isbn13] IS NOT NULL AND [UserId] IS NOT NULL");
        indexes["UX_BookNotFoundReports_OpenPerMemberRareBook"].IsUnique.Should().BeTrue();
        indexes["UX_BookNotFoundReports_OpenPerMemberRareBook"].GetFilter()
            .Should().Be("[Status] = 0 AND [RareBookId] IS NOT NULL AND [UserId] IS NOT NULL");
    }

    [Fact]
    public async Task Save_TwoOpenReportsSameMemberSameIsbn_Throws()
    {
        await using var connection = await OpenConnectionAsync();
        await using var context = await CreateContextAsync(connection);
        Isbn13.TryCreate("9782070612758", out var isbn).Should().BeTrue();
        var member = CreateMember();
        context.Users.Add(member);
        context.Add(BookNotFoundReport.CreateForEdition(Guid.NewGuid(), member.Id, isbn, null, null, Now));
        context.Add(BookNotFoundReport.CreateForEdition(Guid.NewGuid(), member.Id, isbn, null, null, Now));

        var act = () => context.SaveChangesAsync();

        await act.Should().ThrowAsync<DbUpdateException>();
    }

    private static User CreateMember()
    {
        var memberId = UserId.CreateUnique();
        return User.CreateFromExternalIdentity(
            memberId,
            $"test:{memberId.Value:N}",
            $"{memberId.Value:N}@example.test",
            Now);
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

    private static async Task<TestDbContext> CreateContextAsync(SqliteConnection connection)
    {
        var options = new DbContextOptionsBuilder<ProjectDbContext>()
            .UseSqlite(connection)
            .Options;
        var context = new TestDbContext(options);
        await context.Database.EnsureCreatedAsync();
        return context;
    }

    private sealed class TestDbContext(DbContextOptions<ProjectDbContext> options)
        : ProjectDbContext(options)
    {
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<Vole_Papillon_Damour.Domain.BookAggregate.Book>()
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
