using ErrorOr;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using Vole_Papillon_Damour.Application.Common.Interfaces.Persistence;
using Vole_Papillon_Damour.Application.Common.Interfaces.Services;
using Vole_Papillon_Damour.Application.WatchlistFeature.Commands.UnsubscribeMemberAlerts;
using Vole_Papillon_Damour.Application.WatchlistFeature.Common;
using Vole_Papillon_Damour.Domain.AssoEventsAggregate;
using Vole_Papillon_Damour.Domain.AssociationSettingsAggregate;
using Vole_Papillon_Damour.Domain.BookAggregate;
using Vole_Papillon_Damour.Domain.BookAggregate.Entities;
using Vole_Papillon_Damour.Domain.BookMovementAggregate;
using Vole_Papillon_Damour.Domain.OrderAggregate;
using Vole_Papillon_Damour.Domain.ProductAggregate;
using Vole_Papillon_Damour.Domain.RareBookAggregate;
using Vole_Papillon_Damour.Domain.RareBookAggregate.Entities;
using Vole_Papillon_Damour.Domain.ScanSessionAggregate;
using Vole_Papillon_Damour.Domain.UserAggregate;
using Vole_Papillon_Damour.Domain.UserAggregate.ValueObjects;
using Vole_Papillon_Damour.Domain.WatchlistAggregate;
using Vole_Papillon_Damour.Domain.WatchlistAggregate.ValueObjects;

namespace Vole_Papillon_Damour.Application.tests.WatchlistCommands.UnsubscribeMemberAlerts;

public sealed class UnsubscribeMemberAlertsCommandHandlerTests
{
    private static readonly UserId MemberId =
        UserId.Create(Guid.Parse("00000000-0000-0000-0000-000000000042"));

    private static readonly DateTime CreatedAt = new(2026, 9, 3, 17, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime UnsubscribedAt = new(2026, 9, 17, 9, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task Handle_SuspendsAlertsForAnActiveMember()
    {
        await using var fixture = await Fixture.CreateAsync();
        await fixture.AddWatchlistAsync();

        var result = await Handle(fixture);

        result.IsError.Should().BeFalse();
        result.Value.Outcome.Should().Be(UnsubscribeMemberAlertsOutcome.Unsubscribed);
        var watchlist = await fixture.Context.Watchlists.SingleAsync();
        watchlist.AlertStatus.Should().Be(WatchlistAlertStatus.Suspended);
        watchlist.UpdatedAt.Should().Be(UnsubscribedAt);
    }

    [Fact]
    public async Task Handle_IsIdempotentWhenTheMemberAlreadyUnsubscribed()
    {
        await using var fixture = await Fixture.CreateAsync();
        await fixture.AddWatchlistAsync(status: WatchlistAlertStatus.Suspended);

        var result = await Handle(fixture);

        result.Value.Outcome.Should().Be(UnsubscribeMemberAlertsOutcome.AlreadyUnsubscribed);
        (await fixture.Context.Watchlists.SingleAsync())
            .AlertStatus.Should().Be(WatchlistAlertStatus.Suspended);
    }

    [Fact]
    public async Task Handle_LeavesABlockedMemberBlockedInsteadOfDowngradingTheStatus()
    {
        await using var fixture = await Fixture.CreateAsync();
        await fixture.AddWatchlistAsync(status: WatchlistAlertStatus.Blocked);

        var result = await Handle(fixture);

        result.Value.Outcome.Should().Be(UnsubscribeMemberAlertsOutcome.AlreadyUnsubscribed);
        (await fixture.Context.Watchlists.SingleAsync())
            .AlertStatus.Should().Be(WatchlistAlertStatus.Blocked);
    }

    [Fact]
    public async Task Handle_IgnoresATokenWhoseMemberNoLongerExists()
    {
        // The token outlives the account: a deleted member must not turn the
        // one-click POST into an error the provider would keep retrying.
        await using var fixture = await Fixture.CreateAsync();

        var result = await Handle(fixture);

        result.IsError.Should().BeFalse();
        result.Value.Outcome.Should().Be(UnsubscribeMemberAlertsOutcome.IgnoredUnknownMember);
    }

    private static async Task<ErrorOr<UnsubscribeMemberAlertsResult>> Handle(Fixture fixture)
    {
        var clock = Substitute.For<IDateTimeProvider>();
        clock.UtcNow.Returns(UnsubscribedAt);
        var handler = new UnsubscribeMemberAlertsCommandHandler(fixture.Context, clock);
        return await handler.Handle(
            new UnsubscribeMemberAlertsCommand(MemberId.Value),
            CancellationToken.None);
    }

    private sealed class Fixture : IAsyncDisposable
    {
        private readonly SqliteConnection _connection;

        private Fixture(SqliteConnection connection, TestDbContext context)
        {
            _connection = connection;
            Context = context;
        }

        public TestDbContext Context { get; }

        public static async Task<Fixture> CreateAsync()
        {
            var connection = new SqliteConnection("Data Source=:memory:");
            await connection.OpenAsync();
            var context = new TestDbContext(new DbContextOptionsBuilder<TestDbContext>()
                .UseSqlite(connection)
                .Options);
            await context.Database.EnsureCreatedAsync();
            return new Fixture(connection, context);
        }

        public async Task AddWatchlistAsync(WatchlistAlertStatus? status = null)
        {
            var watchlist = Watchlist.Create(MemberId, CreatedAt);
            if (status == WatchlistAlertStatus.Suspended)
            {
                watchlist.SuspendAlerts(CreatedAt);
            }
            else if (status == WatchlistAlertStatus.Blocked)
            {
                watchlist.BlockAlerts(CreatedAt);
            }

            Context.Watchlists.Add(watchlist);
            await Context.SaveChangesAsync();
        }

        public async ValueTask DisposeAsync()
        {
            await Context.DisposeAsync();
            await _connection.DisposeAsync();
        }
    }

    private sealed class TestDbContext(DbContextOptions<TestDbContext> options)
        : DbContext(options), IProjectDbContext
    {
        public DbSet<Watchlist> Watchlists => Set<Watchlist>();

        DbSet<Watchlist> IProjectDbContext.Watchlists => Watchlists;
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
        DbSet<EmailBounceEvent> IProjectDbContext.EmailBounceEvents => throw new NotSupportedException();
        DbSet<RareBook> IProjectDbContext.RareBooks => throw new NotSupportedException();
        DbSet<RareBookPhoto> IProjectDbContext.RareBookPhotos => throw new NotSupportedException();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Ignore<RareBook>();
            modelBuilder.Ignore<RareBookPhoto>();
            modelBuilder.Ignore<Product>();
            modelBuilder.Ignore<User>();
            modelBuilder.Ignore<AssoEvents>();
            modelBuilder.Ignore<Order>();
            modelBuilder.Ignore<Book>();
            modelBuilder.Ignore<BookAnnouncement>();
            modelBuilder.Ignore<BookMovement>();
            modelBuilder.Ignore<ScanSession>();
            modelBuilder.Ignore<AssociationSettings>();
            modelBuilder.Ignore<WatchlistItem>();
            modelBuilder.Ignore<UserAlertHistory>();
            modelBuilder.Ignore<EmailBounceEvent>();

            modelBuilder.Entity<Watchlist>(builder =>
            {
                builder.HasKey(watchlist => watchlist.Id);
                builder.Property(watchlist => watchlist.Id)
                    .HasColumnName("UserId")
                    .ValueGeneratedNever()
                    .HasConversion(id => id.Value, value => UserId.Create(value));
                builder.Property(watchlist => watchlist.AlertStatus).HasConversion<byte>();
                builder.Property(watchlist => watchlist.CreatedAt).HasColumnType("datetime2");
                builder.Property(watchlist => watchlist.UpdatedAt).HasColumnType("datetime2");
            });
        }
    }
}
