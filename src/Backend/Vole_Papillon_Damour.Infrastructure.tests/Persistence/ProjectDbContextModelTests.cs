using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Vole_Papillon_Damour.Domain.AssociationSettingsAggregate;
using Vole_Papillon_Damour.Domain.ActualityAggregate;
using Vole_Papillon_Damour.Domain.ActualityAggregate.ValueObjects;
using Vole_Papillon_Damour.Domain.BookAggregate;
using Vole_Papillon_Damour.Domain.BookAggregate.Entities;
using Vole_Papillon_Damour.Domain.BookMovementAggregate;
using Vole_Papillon_Damour.Domain.RareBookAggregate;
using Vole_Papillon_Damour.Domain.RareBookAggregate.Entities;
using Vole_Papillon_Damour.Domain.RareBookAggregate.ValueObjects;
using Vole_Papillon_Damour.Domain.ScanSessionAggregate;
using Vole_Papillon_Damour.Domain.WatchlistAggregate;
using Vole_Papillon_Damour.Application.Common.Interfaces.Persistence;
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
        model.FindEntityType(typeof(EmailBounceEvent))!.GetTableName().Should().Be("EmailBounceEvents");
        model.FindEntityType(typeof(Actuality))!.GetTableName().Should().Be("Actualities");
        model.FindEntityType(typeof(SocialPostImport))!.GetTableName().Should().Be("SocialPostImports");

        var actualities = model.FindEntityType(typeof(Actuality))!;
        actualities.FindProperty(nameof(Actuality.Status))!.GetDefaultValue()
            .Should().Be(ActualityStatus.Published);
        actualities.FindProperty(nameof(Actuality.TitleNeedsReview))!.GetDefaultValue()
            .Should().Be(false);

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
    public void Model_IndexesBookMovementsForDeadStockQuery()
    {
        using var context = CreateContext();
        var model = context.GetService<IDesignTimeModel>().Model;

        var movementIndexes = model.FindEntityType(typeof(BookMovement))!.GetIndexes();
        movementIndexes
            .Should()
            .Contain(index => index.Properties.Select(property => property.Name)
                .SequenceEqual(new[]
                {
                    nameof(BookMovement.Isbn13),
                    nameof(BookMovement.Type),
                    nameof(BookMovement.OccurredAt)
                }));
    }

    [Fact]
    public void Model_ProtectsWatchlistTargetsAndUsesUserIdAsWatchlistKey()
    {
        using var context = CreateContext();
        var model = context.GetService<IDesignTimeModel>().Model;

        var watchlist = model.FindEntityType(typeof(Watchlist))!;
        watchlist.FindProperty(nameof(Watchlist.Id))!.GetColumnName().Should().Be("UserId");

        var watchlistItems = model.FindEntityType(typeof(WatchlistItem))!;
        watchlistItems.FindProperty(nameof(WatchlistItem.Title))!.GetMaxLength().Should().Be(500);
        watchlistItems.FindProperty(nameof(WatchlistItem.Authors))!.GetMaxLength().Should().Be(500);
        watchlistItems.FindProperty(nameof(WatchlistItem.Publisher))!.GetMaxLength().Should().Be(200);
        watchlistItems.FindProperty(nameof(WatchlistItem.CoverUrl))!.GetMaxLength().Should().Be(2048);
        watchlistItems.GetCheckConstraints()
            .Single(constraint => constraint.Name == "CK_WatchlistItems_ExactlyOneTarget")
            .Sql
            .Should()
            .Contain("[Scope] = 0");

        var outbox = model.FindEntityType(typeof(Vole_Papillon_Damour.Infrastructure.Persistence.Outbox.OutboxMessage))!;
        outbox.FindProperty("Kind")!.GetColumnType().Should().Be("tinyint");

        var bounceEventIndexes = model.FindEntityType(typeof(EmailBounceEvent))!.GetIndexes();
        bounceEventIndexes
            .Single(index => index.Properties.Count == 1 &&
                             index.Properties[0].Name == nameof(EmailBounceEvent.ProviderEventId))
            .IsUnique
            .Should()
            .BeTrue();
    }

    [Fact]
    public void Model_EnforcesOneImportPerSocialPost()
    {
        using var context = CreateContext();
        var model = context.GetService<IDesignTimeModel>().Model;

        var imports = model.FindEntityType(typeof(SocialPostImport))!;
        imports.GetIndexes()
            .Should()
            .ContainSingle(index => index.IsUnique && index.Properties.Select(property => property.Name)
                .SequenceEqual(new[]
                {
                    nameof(SocialPostImport.Source),
                    nameof(SocialPostImport.ExternalId),
                }));
    }

    [Fact]
    public void Model_MapsRareBooksAndPhotosWithoutTheLegacyBookFlag()
    {
        using var context = CreateContext();
        var model = context.GetService<IDesignTimeModel>().Model;

        var rareBooks = model.FindEntityType(typeof(RareBook));
        rareBooks.Should().NotBeNull();
        rareBooks!.GetTableName().Should().Be("RareBooks");
        rareBooks.FindProperty(nameof(RareBook.Slug))!.GetMaxLength().Should().Be(RareBookSlug.MaxLength);
        rareBooks.FindProperty(nameof(RareBook.Price))!.GetPrecision().Should().Be(10);
        rareBooks.FindProperty(nameof(RareBook.Price))!.GetScale().Should().Be(2);
        rareBooks.FindProperty(nameof(RareBook.RowVersion))!.IsConcurrencyToken.Should().BeTrue();
        rareBooks.FindProperty(nameof(RareBook.RowVersion))!.ValueGenerated
            .Should().Be(ValueGenerated.OnAddOrUpdate);

        var rareBookIndexes = rareBooks.GetIndexes();
        rareBookIndexes.Should().ContainSingle(index =>
            index.IsUnique &&
            index.Properties.Select(property => property.Name)
                .SequenceEqual(new[] { nameof(RareBook.Slug) }));
        rareBookIndexes.Should().ContainSingle(index =>
            index.IsUnique &&
            index.GetFilter() == "[Isbn13] IS NOT NULL" &&
            index.Properties.Select(property => property.Name)
                .SequenceEqual(new[] { nameof(RareBook.Isbn13) }));
        rareBookIndexes.Should().ContainSingle(index =>
            index.Properties.Select(property => property.Name)
                .SequenceEqual(new[]
                {
                    nameof(RareBook.Status),
                    nameof(RareBook.IsSold),
                    nameof(RareBook.Price)
                }));

        var photos = model.FindEntityType(typeof(RareBookPhoto));
        photos.Should().NotBeNull();
        photos!.GetTableName().Should().Be("RareBookPhotos");
        photos.FindProperty(nameof(RareBookPhoto.BlobName))!.GetMaxLength().Should().Be(1024);
        photos.FindProperty(nameof(RareBookPhoto.Caption))!.GetMaxLength().Should().Be(80);
        photos.FindProperty(nameof(RareBookPhoto.ContentType))!.GetMaxLength().Should().Be(40);
        photos.GetIndexes().Should().ContainSingle(index =>
            index.IsUnique &&
            index.Properties.Select(property => property.Name)
                .SequenceEqual(new[]
                {
                    nameof(RareBookPhoto.RareBookId),
                    nameof(RareBookPhoto.Position)
                }));
        photos.GetForeignKeys()
            .Single(foreignKey => foreignKey.PrincipalEntityType.ClrType == typeof(RareBook))
            .DeleteBehavior
            .Should().Be(DeleteBehavior.Cascade);

        model.FindEntityType(typeof(Book))!
            .FindProperty("IsRare")
            .Should().BeNull();
    }

    [Fact]
    public void PersistenceContractAndMigrationsExposeRareBookStorage()
    {
        typeof(IProjectDbContext).GetProperty("RareBooks").Should().NotBeNull();
        typeof(IProjectDbContext).GetProperty("RareBookPhotos").Should().NotBeNull();
        typeof(ProjectDbContext).GetProperty("RareBooks").Should().NotBeNull();
        typeof(ProjectDbContext).GetProperty("RareBookPhotos").Should().NotBeNull();

        using var context = CreateContext();
        context.Database.GetMigrations()
            .Should().Contain(migration => migration.EndsWith("_AddRareBooks", StringComparison.Ordinal));
    }

    [Fact]
    public void ClientGestureMigrationAddsTheIdempotencyColumnAndIndex()
    {
        var migrationBuilder = new MigrationBuilder("Microsoft.EntityFrameworkCore.SqlServer");
        new ClientGestureMigrationProbe().ApplyUp(migrationBuilder);

        var addColumn = migrationBuilder.Operations
            .OfType<AddColumnOperation>()
            .SingleOrDefault(operation =>
                operation.Table == "RareBooks" &&
                operation.Name == "ClientGestureId");
        addColumn.Should().NotBeNull();
        addColumn!.ClrType.Should().Be(typeof(Guid));
        addColumn.IsNullable.Should().BeTrue();

        var createIndex = migrationBuilder.Operations
            .OfType<CreateIndexOperation>()
            .SingleOrDefault(operation =>
                operation.Table == "RareBooks" &&
                operation.Name == "IX_RareBooks_ClientGestureId");
        createIndex.Should().NotBeNull();
        createIndex!.IsUnique.Should().BeTrue();
        createIndex.Filter.Should().Be("[ClientGestureId] IS NOT NULL");
    }

    private sealed class ClientGestureMigrationProbe :
        Vole_Papillon_Damour.Infrastructure.Migrations.AddRareBookClientGestureId
    {
        public void ApplyUp(MigrationBuilder migrationBuilder) => Up(migrationBuilder);
    }

    private static ProjectDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<ProjectDbContext>()
            .UseSqlServer("Server=(localdb)\\MSSQLLocalDB;Database=VpdModelTests;Trusted_Connection=True;")
            .Options;

        return new ProjectDbContext(options);
    }
}
