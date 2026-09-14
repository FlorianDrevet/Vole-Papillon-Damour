using FluentAssertions;
using NSubstitute;
using Vole_Papillon_Damour.Application.Books.Queries.Admin;
using Vole_Papillon_Damour.Application.Common.Interfaces.Services;
using Vole_Papillon_Damour.Application.Common.Models;
using Vole_Papillon_Damour.Application.tests.Books.Commands.ScanBook;
using Vole_Papillon_Damour.Domain.BookAggregate;
using Vole_Papillon_Damour.Domain.BookMovementAggregate;
using Vole_Papillon_Damour.Domain.BookMovementAggregate.ValueObjects;

namespace Vole_Papillon_Damour.Application.tests.Books.Queries.Admin;

public sealed class GetCatalogAdminOverviewQueryHandlerTests
{
    private static readonly DateTime Now = new(2026, 9, 14, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task Handle_AggregatesStockLedgerDriftAndDeadStock()
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
        fixture.AlertOutbox
            .GetAdminPageAsync(default, default, default, default, default, default)
            .ReturnsForAnyArgs(new BookAlertOutboxAdminPage([], 0, 1, 1));
        var clock = Substitute.For<IDateTimeProvider>();
        clock.UtcNow.Returns(Now);
        var handler = new GetCatalogAdminOverviewQueryHandler(fixture.Context, clock, fixture.AlertOutbox);

        var result = await handler.Handle(
            new GetCatalogAdminOverviewQuery(From: null, To: null),
            CancellationToken.None);

        result.IsError.Should().BeFalse();
        result.Value.Stock.AvailableQuantity.Should().Be(6);
        result.Value.Stock.AvailableTitles.Should().Be(3);
        result.Value.CurrentPeriod.SoldQuantity.Should().Be(1);
        result.Value.CurrentPeriod.SoldTitles.Should().Be(1);
        result.Value.PreviousPeriod.SoldQuantity.Should().Be(0);
        // Only the fiche with no sale, stock above the threshold and a first
        // availability older than 180 days; the announcement correction is ignored.
        result.Value.DeadStockCount.Should().Be(1);
        // Only 9783140464079 disagrees with its ledger (2 in stock, no movement).
        result.Value.InventoryDriftTitleCount.Should().Be(1);
        result.Value.InventoryDriftQuantity.Should().Be(2);
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
