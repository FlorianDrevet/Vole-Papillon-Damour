using FluentAssertions;
using Vole_Papillon_Damour.Application.Books.Queries.Admin;
using Vole_Papillon_Damour.Application.tests.Books.Commands.ScanBook;
using Vole_Papillon_Damour.Domain.BookAggregate;
using Vole_Papillon_Damour.Domain.BookAggregate.ValueObjects;
using Vole_Papillon_Damour.Domain.BookMovementAggregate;
using Vole_Papillon_Damour.Domain.BookMovementAggregate.ValueObjects;
using Vole_Papillon_Damour.Domain.EventsAggregate.ValueObjects;
using Vole_Papillon_Damour.Domain.UserAggregate.ValueObjects;

namespace Vole_Papillon_Damour.Application.tests.Books.Queries.Admin;

public sealed class GetAdminFairStatsQueryHandlerTests
{
    private static readonly UserId VolunteerId = UserId.Create(Guid.Parse("00000000-0000-0000-0000-000000000031"));

    [Fact]
    public async Task Handle_computes_sales_by_genre_and_lists_previous_fairs()
    {
        await using var fixture = await ScanBookFixture.CreateAsync();
        var previousFair = await fixture.AddFairAsync(
            new DateTimeOffset(2025, 3, 1, 0, 0, 0, TimeSpan.Zero),
            new DateTimeOffset(2025, 3, 1, 0, 0, 0, TimeSpan.Zero),
            null,
            null);
        previousFair.SetBookRevenue(300m);
        var fair = await fixture.AddFairAsync(
            new DateTimeOffset(2025, 9, 1, 0, 0, 0, TimeSpan.Zero),
            new DateTimeOffset(2025, 9, 1, 0, 0, 0, TimeSpan.Zero),
            null,
            null);
        fair.SetBookRevenue(400m);
        await fixture.Context.SaveChangesAsync();

        var novel = await fixture.AddBookAsync("9782070363735", 0);
        novel.ApplyAutomaticMetadata(
            new BookMetadataPatch(null, null, null, null, null, null, "Romans", null, new[] {BookMetadataField.Genre}),
            BookMetadataSource.Bnf,
            new DateTime(2025, 8, 1, 0, 0, 0, DateTimeKind.Utc),
            null);
        await fixture.Context.SaveChangesAsync();

        fixture.Context.BookMovements.Add(AddMovement(
            novel, BookMovementType.Sale, new DateTime(2025, 3, 1, 10, 0, 0, DateTimeKind.Utc), previousFair.Id, quantity: -10));
        fixture.Context.BookMovements.Add(AddMovement(
            novel, BookMovementType.Sale, new DateTime(2025, 9, 1, 10, 0, 0, DateTimeKind.Utc), fair.Id, quantity: -20));
        await fixture.Context.SaveChangesAsync();

        var handler = new GetAdminFairStatsQueryHandler(fixture.Context);

        var result = await handler.Handle(new GetAdminFairStatsQuery(fair.Id.Value), CancellationToken.None);

        result.IsError.Should().BeFalse();
        result.Value.SoldQuantity.Should().Be(20);
        result.Value.Revenue.Should().Be(400m);
        result.Value.AverageBasket.Should().Be(20m);
        result.Value.SalesByGenre.Should().ContainSingle(item => item.Genre == "Romans" && item.Quantity == 20);
        result.Value.PreviousFairs.Should().ContainSingle(item => item.FairId == previousFair.Id.Value && item.SoldQuantity == 10);
    }

    [Fact]
    public async Task Handle_returns_not_found_for_an_unknown_fair()
    {
        await using var fixture = await ScanBookFixture.CreateAsync();
        var handler = new GetAdminFairStatsQueryHandler(fixture.Context);

        var result = await handler.Handle(new GetAdminFairStatsQuery(Guid.NewGuid()), CancellationToken.None);

        result.IsError.Should().BeTrue();
    }

    private static BookMovement AddMovement(
        Book book,
        BookMovementType type,
        DateTime occurredAt,
        AssoEventsId fairId,
        int quantity)
    {
        return BookMovement.Create(
            BookMovementId.CreateUnique(),
            book.Id,
            type,
            quantity,
            occurredAt,
            occurredAt.AddMinutes(1),
            false,
            null,
            VolunteerId,
            fairId,
            null,
            Guid.NewGuid(),
            null);
    }
}
