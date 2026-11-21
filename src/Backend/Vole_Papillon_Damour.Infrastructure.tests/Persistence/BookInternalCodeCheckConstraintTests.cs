using System.Data.Common;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Vole_Papillon_Damour.Domain.BookAggregate;
using Vole_Papillon_Damour.Domain.BookAggregate.ValueObjects;
using Vole_Papillon_Damour.Infrastructure.Persistence;

namespace Vole_Papillon_Damour.Infrastructure.tests.Persistence;

// Books.IsInternalCode ne peut pas contredire la clé (RG-170) : la base refuse un drapeau incohérent.
public sealed class BookInternalCodeCheckConstraintTests
{
    private static readonly DateTime Now = new(2026, 11, 20, 10, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task Database_RejectsAnInternalFlagOnAnIsbnBook()
    {
        await using var connection = await OpenConnectionAsync();
        await using var context = await CreateContextAsync(connection);
        context.Books.Add(Book.Create(IsbnCode("9782070363735"), Now));
        await context.SaveChangesAsync();

        var update = () => context.Database.ExecuteSqlRawAsync(
            "UPDATE Books SET IsInternalCode = 1 WHERE Isbn13 = '9782070363735'");

        await update.Should().ThrowAsync<DbException>();
    }

    [Fact]
    public async Task Database_RejectsANonInternalFlagOnAnInternalCodeBook()
    {
        await using var connection = await OpenConnectionAsync();
        await using var context = await CreateContextAsync(connection);
        context.Books.Add(Book.CreateWithInternalCode(BookCode.CreateInternalFromSequence(1), Now));
        await context.SaveChangesAsync();

        var update = () => context.Database.ExecuteSqlRawAsync(
            "UPDATE Books SET IsInternalCode = 0 WHERE Isbn13 = '2000000000015'");

        await update.Should().ThrowAsync<DbException>();
    }

    [Fact]
    public async Task Database_AcceptsConsistentFlagsForBothKinds()
    {
        await using var connection = await OpenConnectionAsync();
        await using var context = await CreateContextAsync(connection);
        context.Books.Add(Book.Create(IsbnCode("9782070363735"), Now));
        context.Books.Add(Book.CreateWithInternalCode(BookCode.CreateInternalFromSequence(1), Now));

        var save = () => context.SaveChangesAsync();

        await save.Should().NotThrowAsync();
        (await context.Books.CountAsync()).Should().Be(2);
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
        var context = new SqliteTestDbContext(options);
        await context.Database.EnsureCreatedAsync();
        return context;
    }

    private static BookCode IsbnCode(string value)
    {
        Isbn13.TryCreate(value, out var isbn13).Should().BeTrue();
        return isbn13;
    }

    // Même adaptation que les autres tests SQLite : SQLite ne connaît pas nvarchar(max) ni le rowversion SQL Server.
    private sealed class SqliteTestDbContext(DbContextOptions<ProjectDbContext> options)
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
