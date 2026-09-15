using FluentAssertions;
using NSubstitute;
using Vole_Papillon_Damour.Application.Books.Queries.Admin;
using Vole_Papillon_Damour.Application.Common.Interfaces.Services;
using Vole_Papillon_Damour.Application.tests.Books.Commands.ScanBook;
using Vole_Papillon_Damour.Domain.BookAggregate;
using Vole_Papillon_Damour.Domain.BookMovementAggregate;
using Vole_Papillon_Damour.Domain.BookMovementAggregate.ValueObjects;
using Vole_Papillon_Damour.Domain.EventsAggregate.ValueObjects;
using Vole_Papillon_Damour.Domain.UserAggregate.ValueObjects;

namespace Vole_Papillon_Damour.Application.tests.Books.Queries.Admin;

public sealed class GetAdminFairsEvolutionQueryHandlerTests
{
    private static readonly DateTime GeneratedAt = new(2026, 9, 12, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task Handle_orders_fairs_chronologically_and_computes_variation()
    {
        await using var fixture = await ScanBookFixture.CreateAsync();
        var firstFair = await fixture.AddFairAsync(
            new DateTimeOffset(2025, 3, 1, 0, 0, 0, TimeSpan.Zero),
            new DateTimeOffset(2025, 3, 1, 0, 0, 0, TimeSpan.Zero),
            null,
            null);
        firstFair.SetBookRevenue(500m);
        var secondFair = await fixture.AddFairAsync(
            new DateTimeOffset(2025, 9, 1, 0, 0, 0, TimeSpan.Zero),
            new DateTimeOffset(2025, 9, 1, 0, 0, 0, TimeSpan.Zero),
            null,
            null);
        secondFair.SetBookRevenue(1000m);
        await fixture.Context.SaveChangesAsync();

        var book = await fixture.AddBookAsync("9782070363735", 0);
        fixture.Context.BookMovements.Add(AddMovement(
            book, BookMovementType.Sale, new DateTime(2025, 3, 1, 10, 0, 0, DateTimeKind.Utc), firstFair.Id, quantity: -100));
        fixture.Context.BookMovements.Add(AddMovement(
            book, BookMovementType.Sale, new DateTime(2025, 9, 1, 10, 0, 0, DateTimeKind.Utc), secondFair.Id, quantity: -150));
        await fixture.Context.SaveChangesAsync();

        var clock = Substitute.For<IDateTimeProvider>();
        clock.UtcNow.Returns(GeneratedAt);
        var handler = new GetAdminFairsEvolutionQueryHandler(fixture.Context, clock);

        var result = await handler.Handle(new GetAdminFairsEvolutionQuery(), CancellationToken.None);

        result.IsError.Should().BeFalse();
        result.Value.FairCount.Should().Be(2);
        result.Value.Fairs.Should().HaveCount(2);
        result.Value.Fairs[0].FairId.Should().Be(firstFair.Id.Value);
        result.Value.Fairs[0].SoldQuantity.Should().Be(100);
        result.Value.Fairs[0].VariationPercent.Should().BeNull();
        result.Value.Fairs[1].FairId.Should().Be(secondFair.Id.Value);
        result.Value.Fairs[1].SoldQuantity.Should().Be(150);
        result.Value.Fairs[1].VariationPercent.Should().Be(50m);
        result.Value.TotalSoldQuantity.Should().Be(250);
        result.Value.TotalRevenue.Should().Be(1500m);
        result.Value.GrowthSinceFirstPercent.Should().Be(50m);
    }

    [Fact]
    public async Task Handle_restricts_to_the_selected_fair_and_keeps_its_variation()
    {
        await using var fixture = await ScanBookFixture.CreateAsync();
        var firstFair = await fixture.AddFairAsync(
            new DateTimeOffset(2025, 3, 1, 0, 0, 0, TimeSpan.Zero),
            new DateTimeOffset(2025, 3, 1, 0, 0, 0, TimeSpan.Zero),
            null,
            null);
        var secondFair = await fixture.AddFairAsync(
            new DateTimeOffset(2025, 9, 1, 0, 0, 0, TimeSpan.Zero),
            new DateTimeOffset(2025, 9, 1, 0, 0, 0, TimeSpan.Zero),
            null,
            null);
        var book = await fixture.AddBookAsync("9782070363735", 0);
        fixture.Context.BookMovements.Add(AddMovement(
            book, BookMovementType.Sale, new DateTime(2025, 3, 1, 10, 0, 0, DateTimeKind.Utc), firstFair.Id, quantity: -100));
        fixture.Context.BookMovements.Add(AddMovement(
            book, BookMovementType.Sale, new DateTime(2025, 9, 1, 10, 0, 0, DateTimeKind.Utc), secondFair.Id, quantity: -150));
        await fixture.Context.SaveChangesAsync();

        var clock = Substitute.For<IDateTimeProvider>();
        clock.UtcNow.Returns(GeneratedAt);
        var handler = new GetAdminFairsEvolutionQueryHandler(fixture.Context, clock);

        var result = await handler.Handle(
            new GetAdminFairsEvolutionQuery(FairId: secondFair.Id.Value),
            CancellationToken.None);

        result.IsError.Should().BeFalse();
        result.Value.FairCount.Should().Be(1);
        result.Value.Fairs.Should().ContainSingle(fair => fair.FairId == secondFair.Id.Value);
        result.Value.Fairs[0].VariationPercent.Should().Be(50m);
        result.Value.TotalSoldQuantity.Should().Be(150);
        result.Value.GrowthSinceFirstPercent.Should().BeNull();
    }

    [Fact]
    public async Task Handle_rejects_an_unknown_fair()
    {
        await using var fixture = await ScanBookFixture.CreateAsync();
        var clock = Substitute.For<IDateTimeProvider>();
        clock.UtcNow.Returns(GeneratedAt);
        var handler = new GetAdminFairsEvolutionQueryHandler(fixture.Context, clock);

        var result = await handler.Handle(
            new GetAdminFairsEvolutionQuery(FairId: Guid.NewGuid()),
            CancellationToken.None);

        result.IsError.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_rejects_an_invalid_period()
    {
        await using var fixture = await ScanBookFixture.CreateAsync();
        var clock = Substitute.For<IDateTimeProvider>();
        clock.UtcNow.Returns(GeneratedAt);
        var handler = new GetAdminFairsEvolutionQueryHandler(fixture.Context, clock);

        var result = await handler.Handle(
            new GetAdminFairsEvolutionQuery(
                new DateTimeOffset(GeneratedAt),
                new DateTimeOffset(GeneratedAt.AddDays(-1))),
            CancellationToken.None);

        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("Book.InvalidPeriod");
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
            UserId.Create(Guid.Parse("00000000-0000-0000-0000-000000000099")),
            fairId,
            null,
            Guid.NewGuid(),
            null);
    }
}
