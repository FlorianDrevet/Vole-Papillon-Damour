using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Vole_Papillon_Damour.Domain.BookAggregate;
using Vole_Papillon_Damour.Domain.IsbndbAggregate;
using Vole_Papillon_Damour.Infrastructure.Persistence;
using Vole_Papillon_Damour.Infrastructure.Services.Isbndb;

namespace Vole_Papillon_Damour.Infrastructure.tests.Isbndb;

public sealed class IsbndbQuotaGuardTests
{
    [Fact]
    public async Task Guard_PersistsObservedQuotaUsageAndFailureState()
    {
        await using var connection = await OpenConnectionAsync();
        await using var context = await CreateContextAsync(connection);
        using var cache = new MemoryCache(new MemoryCacheOptions());
        var now = new DateTime(2026, 10, 23, 12, 0, 0, DateTimeKind.Utc);
        var guard = new IsbndbQuotaGuard(
            context,
            cache,
            new FixedTimeProvider(now),
            NullLogger<IsbndbQuotaGuard>.Instance);

        (await guard.IsExhaustedAsync(now, CancellationToken.None)).Should().BeFalse();
        await guard.ObserveAsync(5_000, 0, now.AddHours(12), now, CancellationToken.None);
        (await guard.IsExhaustedAsync(now, CancellationToken.None)).Should().BeTrue();
        await guard.RecordUsageAsync(IsbndbUsage.PublicSearch, 3, CancellationToken.None);
        await guard.RecordFailureAsync("HTTP 503", now, CancellationToken.None);

        var quota = await context.IsbndbQuotaStates.SingleAsync();
        quota.DailyLimit.Should().Be(5_000);
        quota.DailyRemaining.Should().Be(0);
        quota.ResetsAt.Should().Be(now.AddHours(12));
        quota.ExhaustedUntil.Should().Be(now.AddHours(12));
        quota.LastFailureAt.Should().Be(now);
        quota.LastFailureReason.Should().Be("HTTP 503");

        var usage = await context.IsbndbDailyUsages.SingleAsync();
        usage.Day.Should().Be(new DateOnly(2026, 10, 23));
        usage.Usage.Should().Be(IsbndbUsage.PublicSearch);
        usage.Credits.Should().Be(3);
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

    private sealed class FixedTimeProvider(DateTime now) : TimeProvider
    {
        private readonly DateTimeOffset _now = new(DateTime.SpecifyKind(now, DateTimeKind.Utc));
        public override DateTimeOffset GetUtcNow() => _now;
    }
}
