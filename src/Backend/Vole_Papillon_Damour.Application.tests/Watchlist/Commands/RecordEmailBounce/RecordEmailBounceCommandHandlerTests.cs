using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Vole_Papillon_Damour.Application.Common.Interfaces.Persistence;
using Vole_Papillon_Damour.Application.WatchlistFeature.Commands.RecordEmailBounce;
using Vole_Papillon_Damour.Domain.ActualityAggregate;
using Vole_Papillon_Damour.Domain.AssoEventsAggregate;
using Vole_Papillon_Damour.Domain.AssociationSettingsAggregate;
using Vole_Papillon_Damour.Domain.BookAggregate;
using Vole_Papillon_Damour.Domain.BookAggregate.Entities;
using Vole_Papillon_Damour.Domain.BookMovementAggregate;
using Vole_Papillon_Damour.Domain.OrderAggregate;
using Vole_Papillon_Damour.Domain.ProductAggregate;
using Vole_Papillon_Damour.Domain.ScanSessionAggregate;
using Vole_Papillon_Damour.Domain.UserAggregate;
using Vole_Papillon_Damour.Domain.UserAggregate.ValueObjects;
using Vole_Papillon_Damour.Domain.WatchlistAggregate;
using Vole_Papillon_Damour.Domain.WatchlistAggregate.ValueObjects;

namespace Vole_Papillon_Damour.Application.tests.WatchlistCommands.RecordEmailBounce;

public sealed class RecordEmailBounceCommandHandlerTests
{
    private static readonly UserId MemberId =
        UserId.Create(Guid.Parse("00000000-0000-0000-0000-000000000042"));

    private static readonly DateTime CreatedAt =
        new(2026, 9, 3, 17, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task Handle_WhenWatchlistExists_RecordsBounceAndSuspendsAtTheThreshold()
    {
        await using var fixture = await WatchlistFixture.CreateAsync();
        await fixture.AddWatchlistAsync();
        var handler = new RecordEmailBounceCommandHandler(fixture.Context);

        for (var index = 0; index < Watchlist.BounceSuspensionThreshold; index++)
        {
            var result = await handler.Handle(
                new RecordEmailBounceCommand(MemberId),
                CancellationToken.None);

            result.IsError.Should().BeFalse();
        }

        var watchlist = await fixture.Context.Watchlists.SingleAsync();
        watchlist.BounceCount.Should().Be(Watchlist.BounceSuspensionThreshold);
        watchlist.AlertStatus.Should().Be(WatchlistAlertStatus.Suspended);
    }

    [Fact]
    public async Task Handle_WhenWatchlistDoesNotExist_ReturnsNotFound()
    {
        await using var fixture = await WatchlistFixture.CreateAsync();
        var handler = new RecordEmailBounceCommandHandler(fixture.Context);

        var result = await handler.Handle(
            new RecordEmailBounceCommand(MemberId),
            CancellationToken.None);

        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("Watchlist.NotFound");
    }

    private sealed class WatchlistFixture : IAsyncDisposable
    {
        private readonly SqliteConnection _connection;

        private WatchlistFixture(SqliteConnection connection, WatchlistTestDbContext context)
        {
            _connection = connection;
            Context = context;
        }

        public WatchlistTestDbContext Context { get; }

        public static async Task<WatchlistFixture> CreateAsync()
        {
            var connection = new SqliteConnection("Data Source=:memory:");
            await connection.OpenAsync();
            var options = new DbContextOptionsBuilder<WatchlistTestDbContext>()
                .UseSqlite(connection)
                .Options;
            var context = new WatchlistTestDbContext(options);
            await context.Database.EnsureCreatedAsync();
            return new WatchlistFixture(connection, context);
        }

        public async Task AddWatchlistAsync()
        {
            Context.Watchlists.Add(Watchlist.Create(MemberId, CreatedAt));
            await Context.SaveChangesAsync();
        }

        public async ValueTask DisposeAsync()
        {
            await Context.DisposeAsync();
            await _connection.DisposeAsync();
        }
    }

    private sealed class WatchlistTestDbContext(DbContextOptions<WatchlistTestDbContext> options)
        : DbContext(options), IProjectDbContext
    {
        public DbSet<Watchlist> Watchlists => Set<Watchlist>();

        DbSet<Product> IProjectDbContext.Products => throw new NotSupportedException();
        DbSet<User> IProjectDbContext.Users => throw new NotSupportedException();
        DbSet<AssoEvents> IProjectDbContext.AssoEvents => throw new NotSupportedException();
        DbSet<Order> IProjectDbContext.Orders => throw new NotSupportedException();
        DbSet<Book> IProjectDbContext.Books => throw new NotSupportedException();
        DbSet<BookAnnouncement> IProjectDbContext.BookAnnouncements => throw new NotSupportedException();
        DbSet<BookMovement> IProjectDbContext.BookMovements => throw new NotSupportedException();
        DbSet<ScanSession> IProjectDbContext.ScanSessions => throw new NotSupportedException();
        DbSet<AssociationSettings> IProjectDbContext.AssociationSettings => throw new NotSupportedException();
        DbSet<WatchlistItem> IProjectDbContext.WatchlistItems => throw new NotSupportedException();
        DbSet<UserAlertHistory> IProjectDbContext.UserAlertHistories => throw new NotSupportedException();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Ignore<Product>();
            modelBuilder.Ignore<User>();
            modelBuilder.Ignore<Order>();
            modelBuilder.Ignore<Book>();
            modelBuilder.Ignore<BookAnnouncement>();
            modelBuilder.Ignore<BookMovement>();
            modelBuilder.Ignore<ScanSession>();
            modelBuilder.Ignore<AssociationSettings>();
            modelBuilder.Ignore<AssoEvents>();
            modelBuilder.Ignore<WatchlistItem>();
            modelBuilder.Ignore<UserAlertHistory>();

            modelBuilder.Entity<Watchlist>(builder =>
            {
                builder.ToTable("Watchlists");
                builder.HasKey(watchlist => watchlist.Id);
                builder.Property(watchlist => watchlist.Id)
                    .HasColumnName("UserId")
                    .ValueGeneratedNever()
                    .HasConversion(
                        userId => userId.Value,
                        value => UserId.Create(value));
                builder.Property(watchlist => watchlist.AlertStatus)
                    .HasConversion<byte>()
                    .IsRequired();
                builder.Property(watchlist => watchlist.BounceCount)
                    .IsRequired();
                builder.Property(watchlist => watchlist.CreatedAt)
                    .IsRequired();
            });
        }
    }
}
