using FluentAssertions;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using Vole_Papillon_Damour.Application.Books.Queries.Admin;
using Vole_Papillon_Damour.Application.Common.Interfaces.Services;
using Vole_Papillon_Damour.Application.Common.Models;
using Vole_Papillon_Damour.Application.tests.Books.Commands.ScanBook;
using Vole_Papillon_Damour.Domain.BookAggregate;
using Vole_Papillon_Damour.Domain.BookAggregate.Entities;
using Vole_Papillon_Damour.Domain.BookAggregate.ValueObjects;
using Vole_Papillon_Damour.Domain.BookMovementAggregate;
using Vole_Papillon_Damour.Domain.BookMovementAggregate.ValueObjects;
using Vole_Papillon_Damour.Domain.EventsAggregate.ValueObjects;
using Vole_Papillon_Damour.Domain.ScanSessionAggregate;
using Vole_Papillon_Damour.Domain.ScanSessionAggregate.ValueObjects;

namespace Vole_Papillon_Damour.Application.tests.Books.Queries.Admin;

public sealed class GetCatalogAdminOverviewQueryHandlerTests
{
    private static readonly DateTime Now = new(2026, 9, 14, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task Handle_PreservesOverviewMetricsWithoutDashboardAlertOrInventoryDriftAggregates()
    {
        await using var fixture = await ScanBookFixture.CreateAsync();
        var deadStock = await fixture.AddBookAsync("9782070363735", quantityAvailable: 3);
        var sold = await fixture.AddBookAsync("9782070408504", quantityAvailable: 1);
        await fixture.AddBookAsync("9783140464079", quantityAvailable: 2);
        AddMovement(fixture, deadStock, BookMovementType.DirectEntry, 3, Now.AddDays(-200));
        AddMovement(
            fixture,
            deadStock,
            BookMovementType.Correction,
            5,
            Now.AddDays(-100),
            "Announcement.Correction");
        AddMovement(fixture, sold, BookMovementType.DirectEntry, 2, Now.AddDays(-10));
        AddMovement(fixture, sold, BookMovementType.Sale, -1, Now.AddDays(-2));
        await fixture.Context.SaveChangesAsync();
        var clock = Substitute.For<IDateTimeProvider>();
        clock.UtcNow.Returns(Now);
        using var cacheServices = CreateHybridCacheProvider();
        var handler = new GetCatalogAdminOverviewQueryHandler(
            fixture.Context,
            clock,
            cacheServices.GetRequiredService<HybridCache>());

        var result = await handler.Handle(
            new GetCatalogAdminOverviewQuery(From: null, To: null),
            CancellationToken.None);

        result.IsError.Should().BeFalse();
        result.Value.Stock.AvailableQuantity.Should().Be(6);
        result.Value.Stock.AvailableTitles.Should().Be(3);
        result.Value.Stock.AnnouncedQuantity.Should().Be(0);
        result.Value.Stock.AnnouncedTitles.Should().Be(0);
        result.Value.CurrentPeriod.SoldQuantity.Should().Be(1);
        result.Value.CurrentPeriod.SoldTitles.Should().Be(1);
        result.Value.PreviousPeriod.SoldQuantity.Should().Be(0);
        ((int?)result.Value.DeadStockCount).Should().BeNull();
        result.Value.DeadStockComputedAt.Should().BeNull();
        result.Value.UndatedAnnouncementCount.Should().Be(0);
        result.Value.IsbndbAbsentToCompleteCount.Should().Be(0);
        _ = fixture.AlertOutbox.DidNotReceiveWithAnyArgs()
            .GetAdminPageAsync(default, default, default, default, default, default);
    }

    [Fact]
    public async Task Handle_SkipsPerBookDriftScanWhenBuildingPeriodSummaries()
    {
        var sqlCapture = new SqlCaptureInterceptor();
        await using var fixture = await ScanBookFixture.CreateAsync(sqlCapture);
        var previousPeriodBook = await fixture.AddBookAsync("9782070363735", quantityAvailable: 0);
        var currentSession = await fixture.AddSessionAsync(ScanMode.AvailableNow, startedAt: Now.AddDays(-1));
        currentSession.RecordScan(true, Now.AddDays(-1), Now.AddDays(-1));
        currentSession.RecordScan(false, Now.AddDays(-1).AddMinutes(1), Now.AddDays(-1).AddMinutes(1));
        var previousSession = await fixture.AddSessionAsync(ScanMode.AvailableNow, startedAt: Now.AddDays(-35));
        previousSession.RecordScan(true, Now.AddDays(-34), Now.AddDays(-34));
        AddMovement(fixture, previousPeriodBook, BookMovementType.DirectEntry, 2, Now.AddDays(-45));
        AddMovement(fixture, previousPeriodBook, BookMovementType.Sale, -2, Now.AddDays(-35));
        await fixture.Context.SaveChangesAsync();
        var clock = Substitute.For<IDateTimeProvider>();
        clock.UtcNow.Returns(Now);
        using var cacheServices = CreateHybridCacheProvider();
        var handler = new GetCatalogAdminOverviewQueryHandler(
            fixture.Context,
            clock,
            cacheServices.GetRequiredService<HybridCache>());
        sqlCapture.Clear();

        var result = await handler.Handle(
            new GetCatalogAdminOverviewQuery(From: null, To: null),
            CancellationToken.None);

        result.IsError.Should().BeFalse();
        result.Value.CurrentPeriod.ScannedCount.Should().Be(2);
        result.Value.CurrentPeriod.KeptCount.Should().Be(1);
        result.Value.CurrentPeriod.RejectedCount.Should().Be(1);
        result.Value.PreviousPeriod.ScannedCount.Should().Be(1);
        result.Value.PreviousPeriod.KeptCount.Should().Be(1);
        result.Value.PreviousPeriod.RejectedCount.Should().Be(0);
        result.Value.PreviousPeriod.SoldQuantity.Should().Be(2);
        result.Value.PreviousPeriod.SoldTitles.Should().Be(1);
        sqlCapture.Commands.Count.Should().BeLessThanOrEqualTo(6);
        sqlCapture.Commands.Should().NotContain(command =>
            command.Contains("BookMovements", StringComparison.OrdinalIgnoreCase) &&
            command.Contains("QuantityAvailable", StringComparison.OrdinalIgnoreCase) &&
            command.Contains("SUM(", StringComparison.OrdinalIgnoreCase));
        _ = fixture.AlertOutbox.DidNotReceiveWithAnyArgs()
            .GetAdminPageAsync(default, default, default, default, default, default);
    }

    [Fact]
    public async Task Handle_ReusesTheSameOverviewForThirtySeconds()
    {
        var sqlCapture = new SqlCaptureInterceptor();
        await using var fixture = await ScanBookFixture.CreateAsync(sqlCapture);
        var book = await fixture.AddBookAsync("9782070363735", quantityAvailable: 3);
        var announcedBook = await fixture.AddBookAsync("9782070408504", quantityAvailable: 0);
        var fair = await fixture.AddFairAsync(
            new DateTimeOffset(Now.AddDays(-5), TimeSpan.Zero),
            new DateTimeOffset(Now.AddDays(-4), TimeSpan.Zero),
            null,
            null);
        fair.SetBookRevenue(123m);
        var session = await fixture.AddSessionAsync(
            ScanMode.NextFair,
            fair.Id,
            startedAt: Now.AddDays(-1));
        fixture.Context.BookAnnouncements.Add(BookAnnouncement.Create(
            BookAnnouncementId.CreateUnique(),
            book.Id,
            fair.Id,
            4,
            Now.AddDays(-2),
            session.Id));
        fixture.Context.BookAnnouncements.Add(BookAnnouncement.Create(
            BookAnnouncementId.CreateUnique(),
            announcedBook.Id,
            null,
            2,
            Now.AddDays(-1),
            session.Id));
        AddMovement(fixture, book, BookMovementType.Sale, -1, Now.AddDays(-2));
        AddMovement(fixture, book, BookMovementType.Sale, -2, Now.AddDays(-3), assoEventsId: fair.Id);
        await fixture.Context.SaveChangesAsync();
        sqlCapture.Clear();
        var clock = Substitute.For<IDateTimeProvider>();
        clock.UtcNow.Returns(Now);
        using var cacheServices = CreateHybridCacheProvider();
        var handler = new GetCatalogAdminOverviewQueryHandler(
            fixture.Context,
            clock,
            cacheServices.GetRequiredService<HybridCache>());
        var query = new GetCatalogAdminOverviewQuery(From: null, To: null);

        var first = await handler.Handle(query, CancellationToken.None);
        first.IsError.Should().BeFalse();
        sqlCapture.Commands.Should().NotBeEmpty();
        sqlCapture.Commands.Count.Should().BeLessThanOrEqualTo(6);
        sqlCapture.Clear();

        var second = await handler.Handle(query, CancellationToken.None);

        second.IsError.Should().BeFalse();
        second.Value.Should().BeEquivalentTo(first.Value);
        first.Value.Stock.AnnouncedQuantity.Should().Be(6);
        first.Value.Stock.AnnouncedTitles.Should().Be(2);
        first.Value.UndatedAnnouncementCount.Should().Be(1);
        first.Value.CurrentPeriod.SoldQuantity.Should().Be(3);
        first.Value.CurrentPeriod.SoldTitles.Should().Be(1);
        first.Value.LastFair.Should().NotBeNull();
        first.Value.LastFair!.SoldQuantity.Should().Be(2);
        first.Value.LastFair.SoldTitles.Should().Be(1);
        first.Value.LastFair.Revenue.Should().Be(123m);
        sqlCapture.Commands.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_ReportsIndependentIsbnQueueCountsAndKeepsMetadataToReviewCount()
    {
        await using var fixture = await ScanBookFixture.CreateAsync();
        var notFound = await fixture.AddBookAsync("9782070363735", quantityAvailable: 0);
        notFound.RecordMetadataNotFound(Now);
        var failed = await fixture.AddBookAsync("9782070408504", quantityAvailable: 0);
        failed.RecordMetadataProviderFailure(Now, "Fournisseur indisponible.");
        var titlelessAbsent = await fixture.AddBookAsync("9783140464079", quantityAvailable: 0);
        titlelessAbsent.MarkAbsentFromIsbndb(Now);
        var titledAbsent = await fixture.AddBookAsync("9782253004226", quantityAvailable: 0);
        titledAbsent.ApplyManualMetadata(
            new BookMetadataPatch("Livre complété", "Un auteur", null, null, null, null, null, null,
                [BookMetadataField.Title, BookMetadataField.Authors]),
            Now);
        titledAbsent.MarkAbsentFromIsbndb(Now);
        await fixture.Context.SaveChangesAsync();
        fixture.AlertOutbox
            .GetAdminPageAsync(default, default, default, default, default, default)
            .ReturnsForAnyArgs(new BookAlertOutboxAdminPage([], 0, 1, 1));

        var clock = Substitute.For<IDateTimeProvider>();
        clock.UtcNow.Returns(Now);
        var handler = new GetCatalogAdminOverviewQueryHandler(fixture.Context, clock, fixture.AlertOutbox);

        var result = await handler.Handle(new GetCatalogAdminOverviewQuery(), CancellationToken.None);

        result.IsError.Should().BeFalse();
        result.Value.MetadataMissingCount.Should().Be(3, "the overview metadata-to-review count remains unchanged");
        result.Value.IsbndbAbsentToCompleteCount.Should().Be(1);
        result.Value.IsbnNotFoundCount.Should().Be(1);
        result.Value.IsbnFailedCount.Should().Be(1);
    }

    private static void AddMovement(
        ScanBookFixture fixture,
        Book book,
        BookMovementType type,
        int quantity,
        DateTime occurredAt,
        string? note = null,
        AssoEventsId? assoEventsId = null)
    {
        fixture.Context.BookMovements.Add(BookMovement.Create(
            BookMovementId.CreateUnique(),
            book.Id,
            type,
            quantity,
            occurredAt,
            occurredAt,
            clockSuspect: false,
            scanSessionId: null,
            volunteerId: null,
            assoEventsId: assoEventsId,
            note: note,
            clientGestureId: null));
    }

    private static ServiceProvider CreateHybridCacheProvider()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddHybridCache();
        return services.BuildServiceProvider();
    }
}
