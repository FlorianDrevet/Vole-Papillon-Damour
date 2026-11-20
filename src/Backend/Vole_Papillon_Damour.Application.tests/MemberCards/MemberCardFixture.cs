using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using ErrorOr;
using Vole_Papillon_Damour.Application.Common.Interfaces.Persistence;
using Vole_Papillon_Damour.Application.Common.Interfaces.Services;
using Vole_Papillon_Damour.Application.Common.Services;
using Vole_Papillon_Damour.Application.MemberCards.Common;
using Vole_Papillon_Damour.Application.MemberCards.Commands.RotateMyCard;
using Vole_Papillon_Damour.Application.MemberCards.Queries.GetMyCard;
using Vole_Papillon_Damour.Application.MemberCards.Queries.ResolveMemberCard;
using Vole_Papillon_Damour.Domain.AssoEventsAggregate;
using Vole_Papillon_Damour.Domain.AssociationSettingsAggregate;
using Vole_Papillon_Damour.Domain.BookAggregate;
using Vole_Papillon_Damour.Domain.BookAggregate.Entities;
using Vole_Papillon_Damour.Domain.BookMovementAggregate;
using Vole_Papillon_Damour.Domain.Common.Models;
using Vole_Papillon_Damour.Domain.MemberCardAggregate;
using Vole_Papillon_Damour.Domain.MemberSelectionAggregate;
using Vole_Papillon_Damour.Domain.OrderAggregate;
using Vole_Papillon_Damour.Domain.ProductAggregate;
using Vole_Papillon_Damour.Domain.RareBookAggregate;
using Vole_Papillon_Damour.Domain.RareBookAggregate.Entities;
using Vole_Papillon_Damour.Domain.ScanSessionAggregate;
using Vole_Papillon_Damour.Domain.UserAggregate;
using Vole_Papillon_Damour.Domain.UserAggregate.ValueObjects;
using Vole_Papillon_Damour.Domain.WatchlistAggregate;
using Vole_Papillon_Damour.Infrastructure.Persistence.Configurations;
using Vole_Papillon_Damour.Infrastructure.Services.MemberCards;

namespace Vole_Papillon_Damour.Application.tests.MemberCards;

internal sealed class MemberCardFixture : IAsyncDisposable
{
    // Public deterministic test material only; this value is never used by runtime configuration.
    private const string TestSigningKey = "MDEyMzQ1Njc4OWFiY2RlZjAxMjM0NTY3ODlhYmNkZWY=";
    private readonly SqliteConnection _connection;
    private readonly DateTime _now;
    private readonly IRandomIndexSource _random;
    private readonly MemberCardIssuer _issuer;
    private readonly IMemberCardTokenService _tokens;

    private MemberCardFixture(SqliteConnection connection, MemberCardTestDbContext context, DateTime now, int[]? randomSequence)
    {
        _connection = connection;
        Context = context;
        _now = now;
        Clock = new MemberCardTestClock(now);
        _random = new SequenceIndexSource(randomSequence ?? [0, 4271, 1, 1180, 2, 6000, 3, 7000]);
        _issuer = new MemberCardIssuer(_random);
        _tokens = new MemberCardTokenService(Options.Create(new MemberCardTokenOptions { SigningKey = TestSigningKey }));
    }

    public Guid ExternalId { get; } = Guid.Parse("7f0b0f0e-0000-0000-0000-00000000c0de");
    public const string Email = "camille@example.test";

    public MemberCardTestDbContext Context { get; }
    public MemberCardTestClock Clock { get; }
    public string? OtherMemberRecoveryCode { get; private set; }

    public static async Task<MemberCardFixture> CreateAsync(DateTime now, int[]? randomSequence = null)
    {
        var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<MemberCardTestDbContext>()
            .UseSqlite(connection)
            .Options;
        var context = new MemberCardTestDbContext(options);
        await context.Database.EnsureCreatedAsync();
        return new MemberCardFixture(connection, context, now, randomSequence);
    }

    public GetMyCardQueryHandler CreateGetMyCardHandler() =>
        new(Context, CreateIdentityService(), Clock, _issuer, _tokens);

    public RotateMyCardCommandHandler CreateRotateHandler() =>
        new(Context, CreateIdentityService(), Clock, _issuer, _tokens);

    public GetMyCardQuery MyCardQuery(
        string? email = null,
        string? firstName = "Camille",
        string? lastName = "Durand") =>
        new(ExternalId, email ?? Email, firstName, lastName);

    public RotateMyCardCommand RotateCommand() => new(ExternalId, Email, "Camille", "Durand");

    public Task<ErrorOr<MemberCardConfirmation>> Resolve(string credential) =>
        new ResolveMemberCardQueryHandler(Context, _tokens).Handle(new ResolveMemberCardQuery(credential), default);

    public async Task AnonymiseMemberAsync()
    {
        var member = await Context.Users.SingleAsync(user => user.ExternalId == ExternalId.ToString());
        member.Anonymize(_now.AddMinutes(10));
        await Context.SaveChangesAsync();
    }

    public async Task IssueCardForOtherMemberAsync()
    {
        var otherId = UserId.Create(Guid.Parse("00000000-0000-0000-0000-0000000000a2"));
        var other = User.CreateFromExternalIdentity(otherId, otherId.Value.ToString(), "other@example.test", _now);
        Context.Users.Add(other);
        var card = await _issuer.GetOrIssueAsync(Context, otherId, _now, default);
        OtherMemberRecoveryCode = card.RecoveryCode;
        await Context.SaveChangesAsync();
    }

    private MemberIdentityService CreateIdentityService() => new(Context, Clock);

    public async ValueTask DisposeAsync()
    {
        await Context.DisposeAsync();
        await _connection.DisposeAsync();
    }
}

internal sealed class MemberCardTestClock(DateTime utcNow) : IDateTimeProvider
{
    public DateTime UtcNow { get; } = utcNow;
}

internal sealed class SequenceIndexSource(IEnumerable<int> values) : IRandomIndexSource
{
    private readonly Queue<int> _values = new(values);

    public int Next(int exclusiveMaximum)
    {
        var value = _values.Count == 0 ? 0 : _values.Dequeue();
        if (value < 0 || value >= exclusiveMaximum)
        {
            throw new InvalidOperationException("The fixture random sequence is outside the requested range.");
        }

        return value;
    }
}

internal sealed class MemberCardTestDbContext(DbContextOptions<MemberCardTestDbContext> options)
    : DbContext(options), IProjectDbContext
{
    public DbSet<User> Users => Set<User>();
    public DbSet<MemberCard> MemberCards => Set<MemberCard>();

    DbSet<Product> IProjectDbContext.Products => throw new NotSupportedException();
    DbSet<AssoEvents> IProjectDbContext.AssoEvents => throw new NotSupportedException();
    DbSet<Order> IProjectDbContext.Orders => throw new NotSupportedException();
    DbSet<Book> IProjectDbContext.Books => throw new NotSupportedException();
    DbSet<BookAnnouncement> IProjectDbContext.BookAnnouncements => throw new NotSupportedException();
    DbSet<BookMovement> IProjectDbContext.BookMovements => throw new NotSupportedException();
    DbSet<ScanSession> IProjectDbContext.ScanSessions => throw new NotSupportedException();
    DbSet<AssociationSettings> IProjectDbContext.AssociationSettings => throw new NotSupportedException();
    DbSet<Watchlist> IProjectDbContext.Watchlists => throw new NotSupportedException();
    DbSet<WatchlistItem> IProjectDbContext.WatchlistItems => throw new NotSupportedException();
    DbSet<UserAlertHistory> IProjectDbContext.UserAlertHistories => throw new NotSupportedException();
    DbSet<EmailBounceEvent> IProjectDbContext.EmailBounceEvents => throw new NotSupportedException();
    DbSet<RareBook> IProjectDbContext.RareBooks => throw new NotSupportedException();
    DbSet<RareBookPhoto> IProjectDbContext.RareBookPhotos => throw new NotSupportedException();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Ignore<Product>();
        modelBuilder.Ignore<AssoEvents>();
        modelBuilder.Ignore<Order>();
        modelBuilder.Ignore<Book>();
        modelBuilder.Ignore<BookAnnouncement>();
        modelBuilder.Ignore<BookMovement>();
        modelBuilder.Ignore<ScanSession>();
        modelBuilder.Ignore<AssociationSettings>();
        modelBuilder.Ignore<Watchlist>();
        modelBuilder.Ignore<WatchlistItem>();
        modelBuilder.Ignore<UserAlertHistory>();
        modelBuilder.Ignore<EmailBounceEvent>();
        modelBuilder.Ignore<RareBook>();
        modelBuilder.Ignore<RareBookPhoto>();

        modelBuilder.Entity<User>(builder =>
        {
            builder.HasKey(user => user.Id);
            builder.Property(user => user.Id)
                .ValueGeneratedNever()
                .HasConversion(id => id.Value, value => UserId.Create(value));
            builder.Ignore(user => user.Password);
            builder.Ignore(user => user.Salt);
            builder.Ignore(user => user.Role);
            builder.ComplexProperty(user => user.Name);
        });

        modelBuilder.ApplyConfiguration(new MemberCardConfiguration());
        base.OnModelCreating(modelBuilder);
    }
}
