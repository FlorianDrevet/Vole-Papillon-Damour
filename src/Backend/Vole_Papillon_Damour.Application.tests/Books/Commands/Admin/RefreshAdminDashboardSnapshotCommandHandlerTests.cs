using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using Vole_Papillon_Damour.Application.Books.Commands.Admin;
using Vole_Papillon_Damour.Application.Books.Queries.Admin;
using Vole_Papillon_Damour.Application.Common.Interfaces.Services;
using Vole_Papillon_Damour.Application.tests.Books.Commands.ScanBook;
using Vole_Papillon_Damour.Domain.BookAggregate;
using Vole_Papillon_Damour.Domain.BookMovementAggregate;
using Vole_Papillon_Damour.Domain.BookMovementAggregate.ValueObjects;
using Vole_Papillon_Damour.Domain.UserAggregate.ValueObjects;
using AssociationSettingsEntity = Vole_Papillon_Damour.Domain.AssociationSettingsAggregate.AssociationSettings;

namespace Vole_Papillon_Damour.Application.tests.Books.Commands.Admin;

public sealed class RefreshAdminDashboardSnapshotCommandHandlerTests
{
    private static readonly DateTime Now = new(2026, 9, 14, 12, 0, 0, DateTimeKind.Utc);
    private static readonly UserId ActorId = UserId.Create(Guid.Parse("00000000-0000-0000-0000-000000000001"));

    [Fact]
    public async Task Handle_StoresTheCurrentDeadStockCountAndRefreshesTheSingletonAfterThresholdChanges()
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
        var settings = CreateSettings(deadStockMinAgeDays: 180, deadStockMinQuantity: 1);
        fixture.Context.AssociationSettings.Add(settings);
        await fixture.Context.SaveChangesAsync();

        var clock = Substitute.For<IDateTimeProvider>();
        clock.UtcNow.Returns(Now);
        var commandHandler = new RefreshAdminDashboardSnapshotCommandHandler(fixture.Context, clock);

        var firstRefresh = await commandHandler.Handle(
            new RefreshAdminDashboardSnapshotCommand(),
            CancellationToken.None);

        firstRefresh.IsError.Should().BeFalse();
        firstRefresh.Value.DeadStockCount.Should().Be(1);
        firstRefresh.Value.ComputedAt.Should().Be(new DateTimeOffset(Now, TimeSpan.Zero));
        var snapshot = await fixture.Context.AdminDashboardSnapshots.SingleAsync();
        snapshot.DeadStockMinAgeDays.Should().Be(180);
        snapshot.DeadStockMinQuantity.Should().Be(1);

        settings.Update(5, 1, 0, 3, 100, 30, 120, 120, 10, ActorId, Now.AddMinutes(10));
        await fixture.Context.SaveChangesAsync();
        clock.UtcNow.Returns(Now.AddMinutes(10));

        var hybridCacheServices = new ServiceCollection().AddLogging();
        hybridCacheServices.AddHybridCache();
        using var overviewCacheProvider = hybridCacheServices.BuildServiceProvider();
        var overviewHandler = new GetCatalogAdminOverviewQueryHandler(
            fixture.Context,
            clock,
            overviewCacheProvider.GetRequiredService<HybridCache>());
        var overviewBeforeRefresh = await overviewHandler.Handle(
            new GetCatalogAdminOverviewQuery(From: null, To: null),
            CancellationToken.None);

        overviewBeforeRefresh.Value.DeadStockCount.Should().Be(1);
        overviewBeforeRefresh.Value.DeadStockComputedAt.Should().Be(new DateTimeOffset(Now, TimeSpan.Zero));

        var secondRefresh = await commandHandler.Handle(
            new RefreshAdminDashboardSnapshotCommand(),
            CancellationToken.None);

        secondRefresh.IsError.Should().BeFalse();
        secondRefresh.Value.DeadStockCount.Should().Be(0);
        secondRefresh.Value.ComputedAt.Should().Be(new DateTimeOffset(Now.AddMinutes(10), TimeSpan.Zero));
        (await fixture.Context.AdminDashboardSnapshots.CountAsync()).Should().Be(1);
        snapshot.DeadStockMinAgeDays.Should().Be(0);
        snapshot.DeadStockMinQuantity.Should().Be(3);
    }

    private static AssociationSettingsEntity CreateSettings(int deadStockMinAgeDays, int deadStockMinQuantity)
    {
        var settings = AssociationSettingsEntity.Create(ActorId, Now);
        settings.Update(
            5,
            1,
            deadStockMinAgeDays,
            deadStockMinQuantity,
            100,
            30,
            120,
            120,
            10,
            ActorId,
            Now);
        return settings;
    }

    private static void AddMovement(
        ScanBookFixture fixture,
        Book book,
        BookMovementType type,
        int quantity,
        DateTime occurredAt,
        string? note = null)
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
            assoEventsId: null,
            note: note,
            clientGestureId: null));
    }
}
