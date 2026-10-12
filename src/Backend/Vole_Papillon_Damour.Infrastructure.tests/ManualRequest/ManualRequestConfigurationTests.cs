using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Vole_Papillon_Damour.Domain.BookAggregate.ValueObjects;
using Vole_Papillon_Damour.Domain.BookMovementAggregate.ValueObjects;
using Vole_Papillon_Damour.Domain.ManualRequestAggregate;
using Vole_Papillon_Damour.Domain.ScanSessionAggregate.ValueObjects;
using Vole_Papillon_Damour.Domain.UserAggregate.ValueObjects;
using Vole_Papillon_Damour.Infrastructure.Persistence;

namespace Vole_Papillon_Damour.Infrastructure.tests.ManualRequest;

public sealed class ManualRequestConfigurationTests
{
    private static readonly UserId Admin = UserId.Create(Guid.Parse("00000000-0000-0000-0000-0000000000a1"));
    private static readonly UserId Volunteer = UserId.Create(Guid.Parse("00000000-0000-0000-0000-0000000000b2"));
    private static readonly DateTime Now = new(2026, 10, 12, 10, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task Configuration_MapsTables()
    {
        await using var connection = await OpenConnectionAsync();
        await using var context = await CreateContextAsync(connection);
        var requester = context.Model.FindEntityType(typeof(ManualRequester));
        var request = context.Model.FindEntityType(typeof(ManualBookRequest));

        requester.Should().NotBeNull();
        requester!.GetTableName().Should().Be("ManualRequesters");
        requester.FindProperty(nameof(ManualRequester.LastName))!.GetColumnType().Should().Be("nvarchar(100)");
        requester.FindProperty(nameof(ManualRequester.FirstName))!.GetColumnType().Should().Be("nvarchar(100)");
        requester.FindProperty(nameof(ManualRequester.Phone))!.GetColumnType().Should().Be("varchar(16)");
        request.Should().NotBeNull();
        request!.GetTableName().Should().Be("ManualBookRequests");
        request.FindProperty(nameof(ManualBookRequest.Isbn13))!.GetColumnType().Should().Be("char(13)");
        request.FindProperty(nameof(ManualBookRequest.FoundIsbn13))!.GetColumnType().Should().Be("char(13)");
        request.FindProperty(nameof(ManualBookRequest.WorkId))!.GetMaxLength().Should().Be(64);
        request.FindProperty(nameof(ManualBookRequest.Title))!.GetMaxLength().Should().Be(500);
        request.FindProperty(nameof(ManualBookRequest.Authors))!.GetMaxLength().Should().Be(500);
        request.FindProperty(nameof(ManualBookRequest.Publisher))!.GetMaxLength().Should().Be(500);
    }

    [Fact]
    public async Task Version_IsAConcurrencyToken()
    {
        await using var connection = await OpenConnectionAsync();
        await using var context = await CreateContextAsync(connection);
        var entityType = context.Model.FindEntityType(typeof(ManualBookRequest));

        entityType.Should().NotBeNull();
        entityType!.FindProperty(nameof(ManualBookRequest.Version))!.IsConcurrencyToken.Should().BeTrue();
    }

    [Fact]
    public async Task Configuration_DeclaresIndexes()
    {
        await using var connection = await OpenConnectionAsync();
        await using var context = await CreateContextAsync(connection);
        var entityType = context.Model.FindEntityType(typeof(ManualBookRequest));
        entityType.Should().NotBeNull();
        var indexes = entityType!.GetIndexes().ToDictionary(index => index.GetDatabaseName());

        indexes.Should().ContainKey("IX_ManualBookRequests_Status_Isbn13");
        indexes.Should().ContainKey("IX_ManualBookRequests_Status_WorkId");
        indexes.Should().ContainKey("IX_ManualBookRequests_RequesterId");
        indexes.Should().ContainKey("IX_ManualBookRequests_HoldMovementId");
    }

    [Fact]
    public async Task RoundTrip_PersistsAFoundRequest()
    {
        await using var connection = await OpenConnectionAsync();
        await using var context = await CreateContextAsync(connection);
        var isbn = Parse("9782070612758");
        var session = ScanSessionId.CreateUnique();
        var movement = BookMovementId.CreateUnique();
        var requester = ManualRequester.Create(Guid.NewGuid(), "Martin", "Jeanne", "0612345678", true, Admin, Now);
        var request = ManualBookRequest.CreateEdition(Guid.NewGuid(), requester.Id, isbn, "Le Petit Prince", null, null, null, Now);
        request.MarkFoundByScan(isbn, session, movement, Volunteer, Now.AddMinutes(1));
        context.Add(requester);
        context.Add(request);
        await context.SaveChangesAsync();

        await using var reloadedContext = await CreateContextAsync(connection, ensureCreated: false);
        var reloaded = await reloadedContext.ManualBookRequests.SingleAsync();

        reloaded.FoundIsbn13.Should().Be(isbn);
        reloaded.FoundScanSessionId.Should().Be(session);
        reloaded.HoldMovementId.Should().Be(movement);
        reloaded.Version.Should().Be(1);
    }

    [Fact]
    public async Task ConcurrentClaim_SecondSaveFails()
    {
        await using var connection = await OpenConnectionAsync();
        await using var first = await CreateContextAsync(connection);
        var requester = ManualRequester.Create(Guid.NewGuid(), "Martin", "Jeanne", "0612345678", true, Admin, Now);
        var request = ManualBookRequest.CreateEdition(Guid.NewGuid(), requester.Id, Parse("9782070612758"), "Le Petit Prince", null, null, null, Now);
        first.Add(requester);
        first.Add(request);
        await first.SaveChangesAsync();

        await using var second = await CreateContextAsync(connection, ensureCreated: false);
        var firstRequest = await first.ManualBookRequests.SingleAsync();
        var secondRequest = await second.ManualBookRequests.SingleAsync();
        var session = ScanSessionId.CreateUnique();
        var firstMovement = BookMovementId.CreateUnique();
        var secondMovement = BookMovementId.CreateUnique();
        var isbn = Parse("9782070612758");
        firstRequest.MarkFoundByScan(isbn, session, firstMovement, Volunteer, Now.AddMinutes(1));
        secondRequest.MarkFoundByScan(isbn, session, secondMovement, Volunteer, Now.AddMinutes(1));
        await first.SaveChangesAsync();

        var act = () => second.SaveChangesAsync();

        await act.Should().ThrowAsync<DbUpdateConcurrencyException>();
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

    private static async Task<TestDbContext> CreateContextAsync(SqliteConnection connection, bool ensureCreated = true)
    {
        var options = new DbContextOptionsBuilder<ProjectDbContext>()
            .UseSqlite(connection)
            .Options;
        var context = new TestDbContext(options);
        if (ensureCreated)
        {
            await context.Database.EnsureCreatedAsync();
        }

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

    private static Isbn13 Parse(string value)
        => Isbn13.TryCreate(value, out var isbn) ? isbn : throw new InvalidOperationException(value);
}
