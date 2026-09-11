using FluentAssertions;
using NSubstitute;
using Vole_Papillon_Damour.Application.Books.Queries.GetVolunteerStatistics;
using Vole_Papillon_Damour.Application.Common.Interfaces.Persistence;
using Vole_Papillon_Damour.Application.Common.Interfaces.Services;
using Vole_Papillon_Damour.Application.tests.Books.Commands.ScanBook;
using Vole_Papillon_Damour.Domain.BookAggregate;
using Vole_Papillon_Damour.Domain.BookAggregate.ValueObjects;
using Vole_Papillon_Damour.Domain.BookMovementAggregate;
using Vole_Papillon_Damour.Domain.BookMovementAggregate.ValueObjects;
using Vole_Papillon_Damour.Domain.EventsAggregate.ValueObjects;
using Vole_Papillon_Damour.Domain.ScanSessionAggregate.ValueObjects;
using Vole_Papillon_Damour.Domain.UserAggregate.ValueObjects;
using DomainScanSession = Vole_Papillon_Damour.Domain.ScanSessionAggregate.ScanSession;

namespace Vole_Papillon_Damour.Application.tests.Books.Queries.VolunteerStatistics;

public sealed class GetVolunteerStatisticsQueryHandlerTests
{
    private static readonly UserId VolunteerId = UserId.Create(
        Guid.Parse("00000000-0000-0000-0000-000000000001"));

    private static readonly DateTime SessionStartedAt =
        new(2026, 9, 1, 8, 0, 0, DateTimeKind.Utc);

    private static readonly DateTime GeneratedAt =
        new(2026, 9, 11, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task Handle_projects_only_the_authenticated_volunteers_activity_and_nets_voided_sales()
    {
        await using var fixture = await ScanBookFixture.CreateAsync();
        var fair = await fixture.AddFairAsync(
            new DateTimeOffset(2026, 9, 6, 0, 0, 0, TimeSpan.Zero),
            new DateTimeOffset(2026, 9, 7, 0, 0, 0, TimeSpan.Zero),
            null,
            null);
        fair.SetBookRevenue(1000m);

        var session = await fixture.AddSessionAsync(
            ScanMode.AvailableNow,
            volunteerId: VolunteerId,
            startedAt: SessionStartedAt);
        session.RecordScan(kept: true, SessionStartedAt.AddMinutes(5), SessionStartedAt.AddMinutes(6));
        session.RecordScan(kept: true, SessionStartedAt.AddMinutes(15), SessionStartedAt.AddMinutes(16));
        session.RecordScan(kept: false, SessionStartedAt.AddMinutes(25), SessionStartedAt.AddMinutes(26));
        session.Close(ScanCloseReason.Manual, SessionStartedAt.AddHours(2));

        var roman = await fixture.AddBookAsync("9782070363735", quantityAvailable: 0);
        roman.ApplyAutomaticMetadata(
            new BookMetadataPatch(
                "Le Petit Prince",
                "Antoine de Saint-Exupéry",
                null,
                null,
                null,
                "fr",
                "Romans",
                null,
                [BookMetadataField.Genre]),
            BookMetadataSource.OpenLibrary,
            SessionStartedAt,
            rawPayload: null);
        roman.UpdateRareStatus(isRare: true, SessionStartedAt);

        var youth = await fixture.AddBookAsync("9791036377426", quantityAvailable: 0);
        youth.ApplyAutomaticMetadata(
            new BookMetadataPatch(
                "Un album",
                null,
                null,
                null,
                null,
                "fr",
                "Jeunesse",
                null,
                [BookMetadataField.Genre]),
            BookMetadataSource.OpenLibrary,
            SessionStartedAt,
            rawPayload: null);

        var romanScan = AddMovement(
            roman,
            BookMovementType.DirectEntry,
            SessionStartedAt.AddMinutes(5),
            VolunteerId,
            session.Id,
            fair.Id);
        fixture.Context.BookMovements.Add(romanScan);
        fixture.Context.BookMovements.Add(AddMovement(
            youth,
            BookMovementType.DirectEntry,
            SessionStartedAt.AddMinutes(15),
            VolunteerId,
            session.Id,
            fair.Id));
        fixture.Context.BookMovements.Add(AddMovement(
            youth,
            BookMovementType.Rejection,
            SessionStartedAt.AddMinutes(25),
            VolunteerId,
            session.Id,
            fair.Id));

        var sale = AddMovement(
            roman,
            BookMovementType.Sale,
            new DateTime(2026, 9, 6, 10, 0, 0, DateTimeKind.Utc),
            VolunteerId,
            scanSessionId: null,
            fair.Id,
            quantity: -2);
        fixture.Context.BookMovements.Add(sale);
        fixture.Context.BookMovements.Add(AddMovement(
            youth,
            BookMovementType.Sale,
            new DateTime(2026, 9, 6, 10, 10, 0, DateTimeKind.Utc),
            VolunteerId,
            scanSessionId: null,
            fair.Id,
            quantity: -1));
        fixture.Context.BookMovements.Add(AddMovement(
            youth,
            BookMovementType.Correction,
            new DateTime(2026, 9, 6, 10, 20, 0, DateTimeKind.Utc),
            VolunteerId,
            scanSessionId: null,
            fair.Id,
            quantity: 1,
            reversalOfMovementId: sale.Id));

        var otherVolunteer = UserId.Create(Guid.Parse("00000000-0000-0000-0000-000000000002"));
        var otherSession = await fixture.AddSessionAsync(
            ScanMode.AvailableNow,
            volunteerId: otherVolunteer,
            startedAt: SessionStartedAt.AddDays(1));
        otherSession.RecordScan(kept: true, SessionStartedAt.AddDays(1).AddMinutes(5), SessionStartedAt.AddDays(1).AddMinutes(6));
        otherSession.Close(ScanCloseReason.Manual, SessionStartedAt.AddDays(1).AddHours(1));
        fixture.Context.BookMovements.Add(AddMovement(
            roman,
            BookMovementType.Sale,
            new DateTime(2026, 9, 7, 10, 0, 0, DateTimeKind.Utc),
            otherVolunteer,
            scanSessionId: null,
            fair.Id,
            quantity: -6));

        await fixture.Context.SaveChangesAsync();
        fixture.AlertOutbox
            .GetSentItemCountForSessionsAsync(
                Arg.Any<IReadOnlyCollection<Guid>>(),
                Arg.Any<CancellationToken>())
            .Returns(4);

        var clock = Substitute.For<IDateTimeProvider>();
        clock.UtcNow.Returns(GeneratedAt);
        var handler = new GetVolunteerStatisticsQueryHandler(
            fixture.Context,
            clock,
            fixture.AlertOutbox);

        var result = await handler.Handle(
            new GetVolunteerStatisticsQuery(VolunteerId),
            CancellationToken.None);

        result.IsError.Should().BeFalse();
        result.Value.Scan.ScannedCount.Should().Be(3);
        result.Value.Scan.KeptCount.Should().Be(2);
        result.Value.Scan.RejectedCount.Should().Be(1);
        result.Value.Scan.SessionCount.Should().Be(1);
        result.Value.Scan.DurationMinutes.Should().Be(120);
        result.Value.Scan.Impact.RareCount.Should().Be(1);
        result.Value.Scan.Impact.AlertItemCount.Should().Be(4);
        result.Value.Scan.Monthly.Single(month => month.PeriodStart.Month == 9).Kept.Should().Be(2);
        result.Value.Scan.Monthly.Single(month => month.PeriodStart.Month == 9).Rejected.Should().Be(1);
        result.Value.Scan.TopGenres.Should().Contain(genre =>
            genre.Name == "Romans" && genre.Quantity == 1);

        result.Value.Cash.GrossSoldQuantity.Should().Be(3);
        result.Value.Cash.SoldQuantity.Should().Be(2);
        result.Value.Cash.VoidedSaleQuantity.Should().Be(1);
        result.Value.Cash.SaleMovementCount.Should().Be(2);
        result.Value.Cash.FairBreakdown.Should().ContainSingle(fairStats =>
            fairStats.Id == fair.Id.Value && fairStats.NetSoldQuantity == 2);
        result.Value.Cash.EstimatedRevenueShare.Should().Be(250m);
        result.Value.Cash.EstimatedTriAndCashOverlap.Should().Be(2);
    }

    [Fact]
    public async Task Handle_returns_empty_buckets_for_a_volunteer_without_activity()
    {
        await using var fixture = await ScanBookFixture.CreateAsync();
        var clock = Substitute.For<IDateTimeProvider>();
        clock.UtcNow.Returns(GeneratedAt);
        var handler = new GetVolunteerStatisticsQueryHandler(
            fixture.Context,
            clock,
            fixture.AlertOutbox);

        var result = await handler.Handle(
            new GetVolunteerStatisticsQuery(VolunteerId),
            CancellationToken.None);

        result.IsError.Should().BeFalse();
        result.Value.Scan.ScannedCount.Should().Be(0);
        result.Value.Scan.Monthly.Should().HaveCount(12);
        result.Value.Scan.RecentSessions.Should().BeEmpty();
        result.Value.Cash.SoldQuantity.Should().Be(0);
        result.Value.Cash.EstimatedCadencePerHour.Should().BeNull();
    }

    private static BookMovement AddMovement(
        Book book,
        BookMovementType type,
        DateTime occurredAt,
        UserId volunteerId,
        ScanSessionId? scanSessionId,
        AssoEventsId? fairId,
        int quantity = 1,
        BookMovementId? reversalOfMovementId = null)
    {
        var movement = BookMovement.Create(
            BookMovementId.CreateUnique(),
            book.Id,
            type,
            quantity,
            occurredAt,
            occurredAt.AddMinutes(1),
            clockSuspect: false,
            scanSessionId,
            volunteerId,
            fairId,
            note: type == BookMovementType.Correction ? "Sale.Void" : null,
            clientGestureId: Guid.NewGuid(),
            reversalOfMovementId);
        return movement;
    }
}
