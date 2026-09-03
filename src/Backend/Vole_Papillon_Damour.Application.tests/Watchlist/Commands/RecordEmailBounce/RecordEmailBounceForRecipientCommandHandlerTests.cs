using ErrorOr;
using FluentAssertions;
using MediatR;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using Vole_Papillon_Damour.Application.Common.Interfaces.Persistence;
using Vole_Papillon_Damour.Application.WatchlistFeature.Commands.RecordEmailBounce;
using Vole_Papillon_Damour.Application.WatchlistFeature.Common;
using Vole_Papillon_Damour.Domain.Common.Errors;
using Vole_Papillon_Damour.Domain.UserAggregate;
using Vole_Papillon_Damour.Domain.UserAggregate.ValueObjects;
using Vole_Papillon_Damour.Domain.WatchlistAggregate.ValueObjects;

namespace Vole_Papillon_Damour.Application.tests.WatchlistCommands.RecordEmailBounce;

public sealed class RecordEmailBounceForRecipientCommandHandlerTests
{
    private const string Recipient = "member@example.test";
    private const string ProviderEventId = "acs-event-42";

    [Fact]
    public async Task Handle_WhenRecipientBelongsToAMember_DelegatesWithTheMemberIdentity()
    {
        await using var fixture = await UserLookupFixture.CreateAsync();
        var user = await fixture.AddUserAsync(Recipient);
        var sender = Substitute.For<ISender>();
        sender.Send(Arg.Any<RecordEmailBounceCommand>(), Arg.Any<CancellationToken>())
            .Returns(new RecordEmailBounceResult(
                BounceCount: 1,
                AlertStatus: WatchlistAlertStatus.Active,
                AlreadyRecorded: false));
        var handler = new RecordEmailBounceForRecipientCommandHandler(fixture.DbContext, sender);

        var result = await handler.Handle(
            new RecordEmailBounceForRecipientCommand(Recipient, ProviderEventId),
            CancellationToken.None);

        result.IsError.Should().BeFalse();
        result.Value.Outcome.Should().Be(RecordEmailBounceForRecipientOutcome.Recorded);
        await sender.Received(1).Send(
            Arg.Is<RecordEmailBounceCommand>(command =>
                command.MemberId == user.Id && command.ProviderEventId == ProviderEventId),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WhenRecipientIsUnknown_IgnoresTheReport()
    {
        await using var fixture = await UserLookupFixture.CreateAsync();
        var sender = Substitute.For<ISender>();
        var handler = new RecordEmailBounceForRecipientCommandHandler(fixture.DbContext, sender);

        var result = await handler.Handle(
            new RecordEmailBounceForRecipientCommand(Recipient, ProviderEventId),
            CancellationToken.None);

        result.IsError.Should().BeFalse();
        result.Value.Outcome.Should().Be(RecordEmailBounceForRecipientOutcome.IgnoredUnknownRecipient);
        await sender.DidNotReceive().Send(
            Arg.Any<RecordEmailBounceCommand>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WhenMemberHasNoWatchlist_IgnoresTheReport()
    {
        await using var fixture = await UserLookupFixture.CreateAsync();
        var user = await fixture.AddUserAsync(Recipient);
        var sender = Substitute.For<ISender>();
        sender.Send(Arg.Any<RecordEmailBounceCommand>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<ErrorOr<RecordEmailBounceResult>>(
                Errors.Watchlist.NotFound(user.Id.Value)));
        var handler = new RecordEmailBounceForRecipientCommandHandler(fixture.DbContext, sender);

        var result = await handler.Handle(
            new RecordEmailBounceForRecipientCommand(Recipient, ProviderEventId),
            CancellationToken.None);

        result.IsError.Should().BeFalse();
        result.Value.Outcome.Should().Be(RecordEmailBounceForRecipientOutcome.IgnoredWithoutWatchlist);
    }

    [Fact]
    public async Task Handle_WhenBounceWasAlreadyRecorded_ReportsTheReplay()
    {
        await using var fixture = await UserLookupFixture.CreateAsync();
        var user = await fixture.AddUserAsync(Recipient);
        var sender = Substitute.For<ISender>();
        sender.Send(Arg.Any<RecordEmailBounceCommand>(), Arg.Any<CancellationToken>())
            .Returns(new RecordEmailBounceResult(
                BounceCount: 2,
                AlertStatus: WatchlistAlertStatus.Active,
                AlreadyRecorded: true));
        var handler = new RecordEmailBounceForRecipientCommandHandler(fixture.DbContext, sender);

        var result = await handler.Handle(
            new RecordEmailBounceForRecipientCommand(Recipient, ProviderEventId),
            CancellationToken.None);

        result.IsError.Should().BeFalse();
        result.Value.Outcome.Should().Be(RecordEmailBounceForRecipientOutcome.AlreadyRecorded);
        await sender.Received(1).Send(
            Arg.Is<RecordEmailBounceCommand>(command => command.MemberId == user.Id),
            Arg.Any<CancellationToken>());
    }

    private sealed class UserLookupFixture : IAsyncDisposable
    {
        private readonly SqliteConnection _connection;
        private readonly UserLookupDbContext _context;

        private UserLookupFixture(SqliteConnection connection, UserLookupDbContext context)
        {
            _connection = connection;
            _context = context;
            DbContext = Substitute.For<IProjectDbContext>();
            DbContext.Users.Returns(context.Users);
        }

        public IProjectDbContext DbContext { get; }

        public static async Task<UserLookupFixture> CreateAsync()
        {
            var connection = new SqliteConnection("Data Source=:memory:");
            await connection.OpenAsync();
            var options = new DbContextOptionsBuilder<UserLookupDbContext>()
                .UseSqlite(connection)
                .Options;
            var context = new UserLookupDbContext(options);
            await context.Database.EnsureCreatedAsync();
            return new UserLookupFixture(connection, context);
        }

        public async Task<User> AddUserAsync(string email)
        {
            var user = User.Create(
                email,
                "not-used",
                new Name("Test", "Member"),
                "not-used");
            var users = DbContext.Users;
            users.Add(user);
            await _context.SaveChangesAsync();
            return user;
        }

        public async ValueTask DisposeAsync()
        {
            await _context.DisposeAsync();
            await _connection.DisposeAsync();
        }
    }

    private sealed class UserLookupDbContext(DbContextOptions<UserLookupDbContext> options) : DbContext(options)
    {
        public DbSet<User> Users => Set<User>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<User>(builder =>
            {
                builder.ToTable("Users");
                builder.HasKey(user => user.Id);
                builder.Property(user => user.Id)
                    .ValueGeneratedNever()
                    .HasConversion(id => id.Value, value => UserId.Create(value));
                builder.Property(user => user.Email).HasMaxLength(320);
                builder.Property(user => user.CreatedAt).IsRequired();
                builder.Property(user => user.LastSeenAt).IsRequired();
                builder.Property(user => user.AnonymizedAt);
                builder.Ignore(user => user.Name);
                builder.Ignore(user => user.Password);
                builder.Ignore(user => user.Salt);
                builder.Ignore(user => user.Role);
            });
        }
    }
}
