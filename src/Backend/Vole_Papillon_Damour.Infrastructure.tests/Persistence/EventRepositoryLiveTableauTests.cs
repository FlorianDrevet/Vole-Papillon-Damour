using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Vole_Papillon_Damour.Domain.AssoEventsAggregate;
using Vole_Papillon_Damour.Domain.AssoEventsAggregate.ValueObjects;
using Vole_Papillon_Damour.Domain.EventsAggregate.ValueObjects;
using Vole_Papillon_Damour.Infrastructure.Persistence;
using Vole_Papillon_Damour.Infrastructure.Persistence.Repositories;

namespace Vole_Papillon_Damour.Infrastructure.tests.Persistence;

public sealed class EventRepositoryLiveTableauTests
{
    [Fact]
    public async Task GetLiveRevisions_ReturnsTheRevisionOfEachExistingRequestedEvent()
    {
        await using var connection = await OpenConnectionAsync();
        await using var context = await CreateContextAsync(connection);
        var loto = CreateEvent();
        var other = CreateEvent();
        loto.MarkLiveChange();
        loto.MarkLiveChange();
        context.AssoEvents.AddRange(loto, other);
        await context.SaveChangesAsync();
        var repository = new EventRepository(context);

        var revisions = await repository.GetLiveRevisionsAsync(
            [loto.Id, AssoEventsId.CreateUnique()],
            CancellationToken.None);

        revisions.Should().ContainSingle();
        revisions[loto.Id].Should().Be(2);
    }

    [Fact]
    public async Task Update_WhenAnotherRequestChangedTheTableauMeanwhile_IsRejected()
    {
        await using var connection = await OpenConnectionAsync();
        var loto = CreateEvent();
        await using (var seed = await CreateContextAsync(connection))
        {
            seed.AssoEvents.Add(loto);
            await seed.SaveChangesAsync();
        }

        await using var firstRequest = await CreateContextAsync(connection);
        await using var secondRequest = await CreateContextAsync(connection);
        var firstCopy = (await firstRequest.AssoEvents.FindAsync(loto.Id))!;
        var secondCopy = (await secondRequest.AssoEvents.FindAsync(loto.Id))!;

        firstCopy.MarkLiveChange();
        await new EventRepository(firstRequest).UpdateAsync(firstCopy);
        secondCopy.MarkLiveChange();
        var act = () => new EventRepository(secondRequest).UpdateAsync(secondCopy);

        await act.Should().ThrowAsync<DbUpdateConcurrencyException>();
    }

    private static AssoEvents CreateEvent() =>
        AssoEvents.Create(
            "Grand loto",
            new Uri("https://example.com/loto.jpg"),
            new EventsType(EventsType.EventsTypeEnum.Bingo),
            new DateTimeOffset(2026, 11, 15, 13, 0, 0, TimeSpan.Zero),
            null,
            null,
            null,
            null,
            new Adresse(12, "Arras", "Rue du loto", 62000),
            null,
            [],
            "Loto");

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
