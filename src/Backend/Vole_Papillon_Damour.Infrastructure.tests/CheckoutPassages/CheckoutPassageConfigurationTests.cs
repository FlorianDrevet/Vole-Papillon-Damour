using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Vole_Papillon_Damour.Application.Common.Interfaces.Persistence;
using Vole_Papillon_Damour.Domain.BookAggregate;
using Vole_Papillon_Damour.Domain.BookAggregate.ValueObjects;
using Vole_Papillon_Damour.Domain.BookMovementAggregate;
using Vole_Papillon_Damour.Domain.BookMovementAggregate.ValueObjects;
using Vole_Papillon_Damour.Domain.CheckoutPassageAggregate;
using Vole_Papillon_Damour.Domain.CheckoutPassageAggregate.ValueObjects;
using Vole_Papillon_Damour.Domain.RareBookAggregate;
using Vole_Papillon_Damour.Domain.UserAggregate;
using Vole_Papillon_Damour.Domain.UserAggregate.ValueObjects;
using Vole_Papillon_Damour.Infrastructure.Persistence;

namespace Vole_Papillon_Damour.Infrastructure.tests.CheckoutPassages;

public sealed class CheckoutPassageConfigurationTests
{
    private static readonly DateTime Now = new(2026, 3, 14, 15, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task SaveChanges_RejectsTwoLinesForSameSaleMovement()
    {
        await using var connection = await OpenConnectionAsync();
        await using var context = await CreateContextAsync(connection);
        var passageId = Guid.NewGuid();
        var book = CreateBook("9782070612758");
        var movement = CreateSale(book, passageId);
        context.Books.Add(book);
        context.BookMovements.Add(movement);
        context.CheckoutPassages.Add(CheckoutPassage.OpenFromSale(passageId, Now, Now));
        context.CheckoutPassageLines.Add(CheckoutPassageLine.ForOrdinarySale(Guid.NewGuid(), passageId, movement, book));
        context.CheckoutPassageLines.Add(CheckoutPassageLine.ForOrdinarySale(Guid.NewGuid(), passageId, movement, book));

        var act = () => context.SaveChangesAsync();

        await act.Should().ThrowAsync<DbUpdateException>();
    }

    [Fact]
    public async Task SaveChanges_RejectsSameRareBookTwiceInPassage()
    {
        await using var connection = await OpenConnectionAsync();
        await using var context = await CreateContextAsync(connection);
        var passageId = Guid.NewGuid();
        var member = CreateMember();
        var rareBook = RareBook.Create("Édition ancienne", 14.50m, Now, member.Id, authorMention: "Une autrice");
        context.Users.Add(member);
        context.CheckoutPassages.Add(CheckoutPassage.OpenFromSale(passageId, Now, Now));
        context.CheckoutPassageLines.Add(CheckoutPassageLine.ForRareSale(Guid.NewGuid(), passageId, rareBook, Now, null));
        context.CheckoutPassageLines.Add(CheckoutPassageLine.ForRareSale(Guid.NewGuid(), passageId, rareBook, Now, null));

        var act = () => context.SaveChangesAsync();

        await act.Should().ThrowAsync<DbUpdateException>();
    }

    [Fact]
    public async Task DeletingUser_KeepsPassageAndLines()
    {
        await using var connection = await OpenConnectionAsync();
        await using var context = await CreateContextAsync(connection);
        var member = CreateMember();
        var passageId = Guid.NewGuid();
        var rareBook = RareBook.Create("Édition ancienne", 14.50m, Now, member.Id);
        var passage = CheckoutPassage.OpenFromSale(passageId, Now, Now);
        passage.Associate(member.Id, UserId.CreateUnique(), Now);
        context.Users.Add(member);
        context.CheckoutPassages.Add(passage);
        context.CheckoutPassageLines.Add(CheckoutPassageLine.ForRareSale(Guid.NewGuid(), passageId, rareBook, Now, null));
        await context.SaveChangesAsync();

        context.Users.Remove(member);
        await context.SaveChangesAsync();

        var remainingPassage = await context.CheckoutPassages.AsNoTracking().SingleAsync();
        remainingPassage.UserId.Should().BeNull();
        remainingPassage.Status.Should().Be(CheckoutPassageStatus.Associated);
        (await context.CheckoutPassageLines.CountAsync()).Should().Be(1);
    }

    [Fact]
    public void Model_MapsCheckoutPassageIndexesLengthsAndDeleteBehaviors()
    {
        using var context = CreateSqlServerModelContext();
        var model = context.GetService<IDesignTimeModel>().Model;

        typeof(IProjectDbContext).GetProperty(nameof(IProjectDbContext.CheckoutPassages)).Should().NotBeNull();
        typeof(IProjectDbContext).GetProperty(nameof(IProjectDbContext.CheckoutPassageLines)).Should().NotBeNull();
        typeof(ProjectDbContext).GetProperty(nameof(ProjectDbContext.CheckoutPassages)).Should().NotBeNull();
        typeof(ProjectDbContext).GetProperty(nameof(ProjectDbContext.CheckoutPassageLines)).Should().NotBeNull();

        var passages = model.FindEntityType(typeof(CheckoutPassage))!;
        passages.GetTableName().Should().Be("CheckoutPassages");
        passages.FindProperty(nameof(CheckoutPassage.UnresolvedReason))!.GetMaxLength().Should().Be(64);
        var memberHistoryIndex = passages.GetIndexes().Single(index => index.Properties.Select(property => property.Name)
            .SequenceEqual(new[] { nameof(CheckoutPassage.UserId), nameof(CheckoutPassage.OccurredAt) }));
        memberHistoryIndex.IsDescending.Should().NotBeNull();
        memberHistoryIndex.IsDescending!.SequenceEqual(new[] { false, true }).Should().BeTrue();
        passages.GetForeignKeys().Should().ContainSingle(foreignKey =>
            foreignKey.PrincipalEntityType.ClrType == typeof(User) &&
            foreignKey.DeleteBehavior == DeleteBehavior.SetNull);

        var lines = model.FindEntityType(typeof(CheckoutPassageLine))!;
        lines.GetTableName().Should().Be("CheckoutPassageLines");
        lines.FindProperty(nameof(CheckoutPassageLine.Title))!.GetMaxLength().Should().Be(500);
        lines.FindProperty(nameof(CheckoutPassageLine.Authors))!.GetMaxLength().Should().Be(500);
        lines.FindProperty(nameof(CheckoutPassageLine.RequestedIsbn13))!.GetMaxLength().Should().Be(13);
        lines.FindProperty(nameof(CheckoutPassageLine.Publisher))!.GetMaxLength().Should().Be(200);
        lines.FindProperty(nameof(CheckoutPassageLine.PhysicalFormat))!.GetMaxLength().Should().Be(100);
        lines.GetIndexes().Should().ContainSingle(index =>
            index.IsUnique && index.GetFilter() == "[SaleMovementId] IS NOT NULL" &&
            index.Properties.Select(property => property.Name).SequenceEqual(new[] { nameof(CheckoutPassageLine.SaleMovementId) }));
        lines.GetIndexes().Should().ContainSingle(index =>
            index.IsUnique && index.GetFilter() == "[RareBookId] IS NOT NULL" &&
            index.Properties.Select(property => property.Name).SequenceEqual(new[] { nameof(CheckoutPassageLine.CheckoutPassageId), nameof(CheckoutPassageLine.RareBookId) }));
        lines.GetForeignKeys().Should().ContainSingle(foreignKey =>
            foreignKey.PrincipalEntityType.ClrType == typeof(CheckoutPassage) &&
            foreignKey.DeleteBehavior == DeleteBehavior.Cascade);

        var movements = model.FindEntityType(typeof(BookMovement))!;
        movements.FindProperty(nameof(BookMovement.CheckoutPassageId))!.IsNullable.Should().BeTrue();
        movements.GetIndexes().Should().ContainSingle(index =>
            index.GetFilter() == "[CheckoutPassageId] IS NOT NULL" &&
            index.Properties.Select(property => property.Name).SequenceEqual(new[] { nameof(BookMovement.CheckoutPassageId) }));
    }

    private static User CreateMember()
    {
        var id = UserId.CreateUnique();
        return User.CreateFromExternalIdentity(id, $"test:{id.Value:N}", $"{id.Value:N}@example.test", Now);
    }

    private static Book CreateBook(string value)
    {
        Isbn13.TryCreate(value, out var isbn).Should().BeTrue();
        return Book.Create(isbn, Now);
    }

    private static BookMovement CreateSale(Book book, Guid passageId)
    {
        return BookMovement.Create(
            BookMovementId.CreateUnique(),
            book.Isbn13,
            BookMovementType.Sale,
            -1,
            Now,
            Now,
            clockSuspect: false,
            scanSessionId: null,
            volunteerId: null,
            assoEventsId: null,
            note: null,
            clientGestureId: Guid.NewGuid(),
            checkoutPassageId: passageId);
    }

    private static async Task<SqliteConnection> OpenConnectionAsync()
    {
        var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        connection.CreateCollation("Latin1_General_100_CI_AI", (left, right) =>
            string.Compare(left, right, StringComparison.OrdinalIgnoreCase));
        return connection;
    }

    private static async Task<TestDbContext> CreateContextAsync(SqliteConnection connection)
    {
        var options = new DbContextOptionsBuilder<ProjectDbContext>().UseSqlite(connection).Options;
        var context = new TestDbContext(options);
        await context.Database.EnsureCreatedAsync();
        return context;
    }

    private static ProjectDbContext CreateSqlServerModelContext()
    {
        var options = new DbContextOptionsBuilder<ProjectDbContext>()
            .UseSqlServer("Server=(localdb)\\MSSQLLocalDB;Database=VpdCheckoutPassageModelTests;Trusted_Connection=True;")
            .Options;
        return new ProjectDbContext(options);
    }

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

            foreach (var property in modelBuilder.Model.GetEntityTypes()
                         .SelectMany(entityType => entityType.GetProperties())
                         .Where(property => property.GetColumnType() == "nvarchar(max)"))
            {
                property.SetColumnType("TEXT");
            }
        }
    }
}
