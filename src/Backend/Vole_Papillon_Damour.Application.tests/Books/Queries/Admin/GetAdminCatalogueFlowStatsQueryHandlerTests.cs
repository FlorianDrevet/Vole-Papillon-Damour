using FluentAssertions;
using NSubstitute;
using Vole_Papillon_Damour.Application.Books.Queries.Admin;
using Vole_Papillon_Damour.Application.Common.Interfaces.Services;
using Vole_Papillon_Damour.Application.tests.Books.Commands.ScanBook;
using Vole_Papillon_Damour.Domain.BookAggregate;
using Vole_Papillon_Damour.Domain.BookAggregate.ValueObjects;
using Vole_Papillon_Damour.Domain.BookMovementAggregate;
using Vole_Papillon_Damour.Domain.BookMovementAggregate.ValueObjects;
using Vole_Papillon_Damour.Domain.ScanSessionAggregate.ValueObjects;
using Vole_Papillon_Damour.Domain.UserAggregate.ValueObjects;

namespace Vole_Papillon_Damour.Application.tests.Books.Queries.Admin;

public sealed class GetAdminCatalogueFlowStatsQueryHandlerTests
{
    private static readonly DateTime GeneratedAt = new(2026, 9, 12, 12, 0, 0, DateTimeKind.Utc);
    private static readonly UserId VolunteerId = UserId.Create(Guid.Parse("00000000-0000-0000-0000-000000000021"));

    [Fact]
    public async Task Handle_builds_the_funnel_and_the_genre_flow_rate()
    {
        await using var fixture = await ScanBookFixture.CreateAsync();
        var session = await fixture.AddSessionAsync(
            ScanMode.AvailableNow,
            volunteerId: VolunteerId,
            startedAt: new DateTime(2026, 6, 1, 8, 0, 0, DateTimeKind.Utc));
        session.RecordScan(true, new DateTime(2026, 6, 1, 8, 5, 0, DateTimeKind.Utc), new DateTime(2026, 6, 1, 8, 6, 0, DateTimeKind.Utc));
        session.RecordScan(true, new DateTime(2026, 6, 1, 8, 10, 0, DateTimeKind.Utc), new DateTime(2026, 6, 1, 8, 11, 0, DateTimeKind.Utc));

        var novel = await fixture.AddBookAsync("9782070363735", 0);
        novel.ApplyAutomaticMetadata(
            GenrePatch("Romans"), BookMetadataSource.Bnf, GeneratedAt.AddDays(-60), null);
        var comic = await fixture.AddBookAsync("9791036377426", 0);
        comic.ApplyAutomaticMetadata(
            GenrePatch("Bandes dessinées"), BookMetadataSource.Bnf, GeneratedAt.AddDays(-60), null);
        await fixture.Context.SaveChangesAsync();

        fixture.Context.BookMovements.Add(AddMovement(
            novel, BookMovementType.DirectEntry, new DateTime(2026, 6, 1, 8, 5, 0, DateTimeKind.Utc), session.Id));
        fixture.Context.BookMovements.Add(AddMovement(
            comic, BookMovementType.DirectEntry, new DateTime(2026, 6, 1, 8, 10, 0, DateTimeKind.Utc), session.Id));
        fixture.Context.BookMovements.Add(AddMovement(
            novel, BookMovementType.Sale, new DateTime(2026, 6, 15, 10, 0, 0, DateTimeKind.Utc), null, quantity: -1));
        await fixture.Context.SaveChangesAsync();

        var clock = Substitute.For<IDateTimeProvider>();
        clock.UtcNow.Returns(GeneratedAt);
        var handler = new GetAdminCatalogueFlowStatsQueryHandler(fixture.Context, clock);

        var result = await handler.Handle(new GetAdminCatalogueFlowStatsQuery(), CancellationToken.None);

        result.IsError.Should().BeFalse();
        result.Value.Funnel.ScannedCount.Should().Be(2);
        result.Value.Funnel.KeptCount.Should().Be(2);
        result.Value.Funnel.SoldCount.Should().Be(1);
        result.Value.Funnel.DormantCount.Should().Be(1);
        result.Value.FlowRateByGenre.Should().Contain(item =>
            item.Genre == "Romans" && item.KeptQuantity == 1 && item.SoldQuantity == 1 && item.FlowRatePercent == 100m);
        result.Value.FlowRateByGenre.Should().Contain(item =>
            item.Genre == "Bandes dessinées" && item.KeptQuantity == 1 && item.SoldQuantity == 0 && item.FlowRatePercent == 0m);
        result.Value.TimeToSellDistribution.Sum(bucket => bucket.Quantity).Should().Be(1);
    }

    [Fact]
    public async Task Handle_rejects_an_invalid_period()
    {
        await using var fixture = await ScanBookFixture.CreateAsync();
        var clock = Substitute.For<IDateTimeProvider>();
        clock.UtcNow.Returns(GeneratedAt);
        var handler = new GetAdminCatalogueFlowStatsQueryHandler(fixture.Context, clock);

        var result = await handler.Handle(
            new GetAdminCatalogueFlowStatsQuery(
                new DateTimeOffset(GeneratedAt),
                new DateTimeOffset(GeneratedAt.AddDays(-1))),
            CancellationToken.None);

        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("Book.InvalidPeriod");
    }

    private static BookMetadataPatch GenrePatch(string genre) =>
        new(null, null, null, null, null, null, genre, null, new[] {BookMetadataField.Genre});

    private static BookMovement AddMovement(
        Book book,
        BookMovementType type,
        DateTime occurredAt,
        ScanSessionId? scanSessionId,
        int quantity = 1)
    {
        return BookMovement.Create(
            BookMovementId.CreateUnique(),
            book.Id,
            type,
            quantity,
            occurredAt,
            occurredAt.AddMinutes(1),
            false,
            scanSessionId,
            VolunteerId,
            null,
            null,
            Guid.NewGuid(),
            null);
    }
}
