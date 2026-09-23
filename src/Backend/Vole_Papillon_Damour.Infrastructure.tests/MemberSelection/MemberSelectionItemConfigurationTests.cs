using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Vole_Papillon_Damour.Domain.BookAggregate.ValueObjects;
using Vole_Papillon_Damour.Domain.MemberSelectionAggregate;
using Vole_Papillon_Damour.Domain.UserAggregate;
using Vole_Papillon_Damour.Domain.UserAggregate.ValueObjects;
using Vole_Papillon_Damour.Infrastructure.Persistence;

namespace Vole_Papillon_Damour.Infrastructure.tests.MemberSelection;

public sealed class MemberSelectionItemConfigurationTests
{
    private static readonly DateTime Now = new(2026, 9, 23, 10, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task SaveChanges_WhenSameIsbnIsSelectedTwiceByOneMember_Throws()
    {
        await using var connection = await OpenConnectionAsync();
        await using var context = await CreateContextAsync(connection);
        Isbn13.TryCreate("9782070612758", out var isbn).Should().BeTrue();
        var member = CreateMember();
        context.Users.Add(member);
        context.MemberSelectionItems.Add(MemberSelectionItem.CreateForEdition(Guid.NewGuid(), member.Id, isbn, Now));
        context.MemberSelectionItems.Add(MemberSelectionItem.CreateForEdition(Guid.NewGuid(), member.Id, isbn, Now));

        var act = () => context.SaveChangesAsync();

        await act.Should().ThrowAsync<DbUpdateException>();
    }

    [Fact]
    public async Task SaveChanges_WhenTwoMembersSelectSameIsbn_Succeeds()
    {
        await using var connection = await OpenConnectionAsync();
        await using var context = await CreateContextAsync(connection);
        Isbn13.TryCreate("9782070612758", out var isbn).Should().BeTrue();
        var firstMember = CreateMember();
        var secondMember = CreateMember();
        context.Users.AddRange(firstMember, secondMember);
        context.MemberSelectionItems.Add(MemberSelectionItem.CreateForEdition(Guid.NewGuid(), firstMember.Id, isbn, Now));
        context.MemberSelectionItems.Add(MemberSelectionItem.CreateForEdition(Guid.NewGuid(), secondMember.Id, isbn, Now));

        await context.SaveChangesAsync();

        (await context.MemberSelectionItems.CountAsync()).Should().Be(2);
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
