using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Vole_Papillon_Damour.Domain.AssociationSettingsAggregate;
using Vole_Papillon_Damour.Domain.BookAggregate;
using Vole_Papillon_Damour.Domain.BookAggregate.ValueObjects;
using Vole_Papillon_Damour.Domain.BookMovementAggregate;
using Vole_Papillon_Damour.Domain.BookMovementAggregate.ValueObjects;
using Vole_Papillon_Damour.Domain.ScanSessionAggregate;
using Vole_Papillon_Damour.Domain.ScanSessionAggregate.ValueObjects;
using Vole_Papillon_Damour.Domain.UserAggregate;
using Vole_Papillon_Damour.Domain.UserAggregate.ValueObjects;
using Vole_Papillon_Damour.Infrastructure.AccountDeletion;
using Vole_Papillon_Damour.Infrastructure.Persistence;

namespace Vole_Papillon_Damour.Infrastructure.tests.AccountDeletion;

public sealed class NoRetainedSalesMovementsPolicyTests
{
    private static readonly DateTime Now = new(2026, 9, 4, 8, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task HasRetainedSalesMovementsAsync_ReturnsTrue_WhenBookMovementReferencesUser()
    {
        await using var fixture = await Fixture.CreateAsync();
        var user = CreateUser();
        var isbn = ParseIsbn("9780306406157");

        fixture.Context.Users.Add(user);
        fixture.Context.Books.Add(Book.Create(isbn, Now));
        fixture.Context.BookMovements.Add(BookMovement.Create(
            BookMovementId.CreateUnique(),
            isbn,
            BookMovementType.AnnouncementEntry,
            1,
            Now,
            Now,
            false,
            null,
            user.Id,
            null,
            null,
            Guid.NewGuid()));
        await fixture.Context.SaveChangesAsync();

        var result = await new NoRetainedSalesMovementsPolicy(fixture.Context)
            .HasRetainedSalesMovementsAsync(user.Id.Value, CancellationToken.None);

        result.Should().BeTrue();
    }

    [Fact]
    public async Task HasRetainedSalesMovementsAsync_ReturnsTrue_WhenScanSessionReferencesUser()
    {
        await using var fixture = await Fixture.CreateAsync();
        var user = CreateUser();

        fixture.Context.Users.Add(user);
        fixture.Context.ScanSessions.Add(ScanSession.Create(
            user.Id,
            ScanMode.AvailableNow,
            null,
            Now));
        await fixture.Context.SaveChangesAsync();

        var result = await new NoRetainedSalesMovementsPolicy(fixture.Context)
            .HasRetainedSalesMovementsAsync(user.Id.Value, CancellationToken.None);

        result.Should().BeTrue();
    }

    [Fact]
    public async Task HasRetainedSalesMovementsAsync_ReturnsTrue_WhenAssociationSettingsReferenceUser()
    {
        await using var fixture = await Fixture.CreateAsync();
        var user = CreateUser();

        fixture.Context.Users.Add(user);
        fixture.Context.AssociationSettings.Add(AssociationSettings.Create(user.Id, Now));
        await fixture.Context.SaveChangesAsync();

        var result = await new NoRetainedSalesMovementsPolicy(fixture.Context)
            .HasRetainedSalesMovementsAsync(user.Id.Value, CancellationToken.None);

        result.Should().BeTrue();
    }

    [Fact]
    public async Task HasRetainedSalesMovementsAsync_ReturnsFalse_WhenUserHasNoProtectedReferences()
    {
        await using var fixture = await Fixture.CreateAsync();
        var user = CreateUser();
        fixture.Context.Users.Add(user);
        await fixture.Context.SaveChangesAsync();

        var result = await new NoRetainedSalesMovementsPolicy(fixture.Context)
            .HasRetainedSalesMovementsAsync(user.Id.Value, CancellationToken.None);

        result.Should().BeFalse();
    }

    private static User CreateUser()
    {
        return User.Create(
            "member@example.test",
            "unused",
            new Name("Test", "Member"),
            "unused");
    }

    private static Isbn13 ParseIsbn(string value)
    {
        Isbn13.TryCreate(value, out var isbn).Should().BeTrue();
        return isbn;
    }

    private sealed class Fixture : IAsyncDisposable
    {
        private readonly SqliteConnection _connection;

        private Fixture(SqliteConnection connection, ProjectDbContext context)
        {
            _connection = connection;
            Context = context;
        }

        public ProjectDbContext Context { get; }

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
            await _connection.DisposeAsync();
        }
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
