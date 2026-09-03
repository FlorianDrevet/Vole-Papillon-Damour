using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Vole_Papillon_Damour.Domain.AssociationSettingsAggregate;
using Vole_Papillon_Damour.Domain.BookAggregate;
using Vole_Papillon_Damour.Domain.BookAggregate.Entities;
using Vole_Papillon_Damour.Domain.BookMovementAggregate;
using Vole_Papillon_Damour.Domain.ScanSessionAggregate;
using Vole_Papillon_Damour.Domain.WatchlistAggregate;
using Vole_Papillon_Damour.Infrastructure.Persistence;

namespace Vole_Papillon_Damour.Infrastructure.tests.Persistence;

public sealed class ProjectDbContextModelTests
{
    [Fact]
    public void Model_MapsBookExchangeTablesAndConcurrencyFields()
    {
        using var context = CreateContext();
        var model = context.GetService<IDesignTimeModel>().Model;

        model.FindEntityType(typeof(Book))!.GetTableName().Should().Be("Books");
        model.FindEntityType(typeof(BookAnnouncement))!.GetTableName().Should().Be("BookAnnouncements");
        model.FindEntityType(typeof(BookMovement))!.GetTableName().Should().Be("BookMovements");
        model.FindEntityType(typeof(ScanSession))!.GetTableName().Should().Be("ScanSessions");
        model.FindEntityType(typeof(AssociationSettings))!.GetTableName().Should().Be("AssociationSettings");
        model.FindEntityType(typeof(Watchlist))!.GetTableName().Should().Be("Watchlists");
        model.FindEntityType(typeof(WatchlistItem))!.GetTableName().Should().Be("WatchlistItems");
        model.FindEntityType(typeof(UserAlertHistory))!.GetTableName().Should().Be("UserAlertHistory");

        var books = model.FindEntityType(typeof(Book))!;
        books.FindProperty(nameof(Book.RowVersion))!.IsConcurrencyToken.Should().BeTrue();
        books.FindProperty(nameof(Book.RowVersion))!.ValueGenerated.Should().Be(ValueGenerated.OnAddOrUpdate);
        books.FindProperty(nameof(Book.Title))!.GetCollation().Should().Be("Latin1_General_100_CI_AI");
        books.FindProperty(nameof(Book.Authors))!.GetCollation().Should().Be("Latin1_General_100_CI_AI");
    }

    [Fact]
    public void Model_ProtectsGestureIdempotenceAndOpenSessionUniqueness()
    {
        using var context = CreateContext();
        var model = context.GetService<IDesignTimeModel>().Model;

        var movementIndexes = model.FindEntityType(typeof(BookMovement))!.GetIndexes();
        movementIndexes
            .Single(index => index.Properties.Count == 1 && index.Properties[0].Name == nameof(BookMovement.ClientGestureId))
            .IsUnique
            .Should()
            .BeTrue();
        movementIndexes
            .Single(index => index.Properties.Count == 1 && index.Properties[0].Name == nameof(BookMovement.ClientGestureId))
            .GetFilter()
            .Should()
            .Be("[ClientGestureId] IS NOT NULL");

        var sessionIndexes = model.FindEntityType(typeof(ScanSession))!.GetIndexes();
        sessionIndexes
            .Single(index => index.Properties.Count == 1 && index.Properties[0].Name == nameof(ScanSession.VolunteerId))
            .GetFilter()
            .Should()
            .Be("[Status] = 0");
    }

    [Fact]
    public void Model_ProtectsWatchlistTargetsAndUsesUserIdAsWatchlistKey()
    {
        using var context = CreateContext();
        var model = context.GetService<IDesignTimeModel>().Model;

        var watchlist = model.FindEntityType(typeof(Watchlist))!;
        watchlist.FindProperty(nameof(Watchlist.Id))!.GetColumnName().Should().Be("UserId");

        var watchlistItems = model.FindEntityType(typeof(WatchlistItem))!;
        watchlistItems.GetCheckConstraints()
            .Single(constraint => constraint.Name == "CK_WatchlistItems_ExactlyOneTarget")
            .Sql
            .Should()
            .Contain("[Scope] = 0");

        var outbox = model.FindEntityType(typeof(Vole_Papillon_Damour.Infrastructure.Persistence.Outbox.OutboxMessage))!;
        outbox.FindProperty("Kind")!.GetColumnType().Should().Be("tinyint");
    }

    private static ProjectDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<ProjectDbContext>()
            .UseSqlServer("Server=(localdb)\\MSSQLLocalDB;Database=VpdModelTests;Trusted_Connection=True;")
            .Options;

        return new ProjectDbContext(options);
    }
}
