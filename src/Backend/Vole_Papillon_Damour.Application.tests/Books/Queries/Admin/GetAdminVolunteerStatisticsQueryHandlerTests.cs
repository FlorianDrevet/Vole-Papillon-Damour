using FluentAssertions;
using NSubstitute;
using Vole_Papillon_Damour.Application.Books.Queries.Admin;
using Vole_Papillon_Damour.Application.Common.Interfaces.Services;
using Vole_Papillon_Damour.Application.tests.Books.Commands.ScanBook;
using Vole_Papillon_Damour.Domain.BookAggregate;
using Vole_Papillon_Damour.Domain.BookAggregate.ValueObjects;
using Vole_Papillon_Damour.Domain.BookMovementAggregate;
using Vole_Papillon_Damour.Domain.BookMovementAggregate.ValueObjects;
using Vole_Papillon_Damour.Domain.EventsAggregate.ValueObjects;
using Vole_Papillon_Damour.Domain.ScanSessionAggregate.ValueObjects;
using Vole_Papillon_Damour.Domain.UserAggregate;
using Vole_Papillon_Damour.Domain.UserAggregate.ValueObjects;

namespace Vole_Papillon_Damour.Application.tests.Books.Queries.Admin;

public sealed class GetAdminVolunteerStatisticsQueryHandlerTests
{
    private static readonly DateTime GeneratedAt =
        new(2026, 9, 12, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task Handle_aggregates_the_team_and_nets_voided_sales()
    {
        await using var fixture = await ScanBookFixture.CreateAsync();
        var fair = await fixture.AddFairAsync(
            new DateTimeOffset(2026, 9, 10, 0, 0, 0, TimeSpan.Zero),
            new DateTimeOffset(2026, 9, 10, 0, 0, 0, TimeSpan.Zero),
            null,
            null);
        var adaId = UserId.Create(Guid.Parse("00000000-0000-0000-0000-000000000011"));
        var beaId = UserId.Create(Guid.Parse("00000000-0000-0000-0000-000000000012"));
        fixture.Context.Users.Add(User.CreateFromExternalIdentity(
            adaId,
            "ada-external",
            "ada@example.test",
            GeneratedAt.AddDays(-30),
            new Name("Ada", "Lovelace")));
        fixture.Context.Users.Add(User.CreateFromExternalIdentity(
            beaId,
            "bea-external",
            "bea@example.test",
            GeneratedAt.AddDays(-20),
            new Name("Bea", "Booker")));
        await fixture.Context.SaveChangesAsync();

        var adaSession = await fixture.AddSessionAsync(
            ScanMode.NextFair,
            fair.Id,
            adaId,
            new DateTime(2026, 9, 9, 8, 0, 0, DateTimeKind.Utc));
        adaSession.RecordScan(true, new DateTime(2026, 9, 9, 8, 5, 0, DateTimeKind.Utc), new DateTime(2026, 9, 9, 8, 6, 0, DateTimeKind.Utc));
        adaSession.RecordScan(true, new DateTime(2026, 9, 9, 8, 15, 0, DateTimeKind.Utc), new DateTime(2026, 9, 9, 8, 16, 0, DateTimeKind.Utc));
        adaSession.RecordScan(false, new DateTime(2026, 9, 9, 8, 25, 0, DateTimeKind.Utc), new DateTime(2026, 9, 9, 8, 26, 0, DateTimeKind.Utc));
        adaSession.Close(ScanCloseReason.Manual, new DateTime(2026, 9, 9, 10, 0, 0, DateTimeKind.Utc));
        var beaSession = await fixture.AddSessionAsync(
            ScanMode.NextFair,
            fair.Id,
            beaId,
            new DateTime(2026, 9, 9, 9, 0, 0, DateTimeKind.Utc));
        beaSession.RecordScan(true, new DateTime(2026, 9, 9, 9, 5, 0, DateTimeKind.Utc), new DateTime(2026, 9, 9, 9, 6, 0, DateTimeKind.Utc));
        beaSession.Close(ScanCloseReason.Manual, new DateTime(2026, 9, 9, 10, 0, 0, DateTimeKind.Utc));

        var roman = await fixture.AddBookAsync("9782070363735", 0);
        var youth = await fixture.AddBookAsync("9791036377426", 0);
        var oldTitle = await fixture.AddBookAsync("9782070408504", 0);
        fixture.Context.BookMovements.Add(AddMovement(
            roman, BookMovementType.DirectEntry, adaSession.StartedAt.AddMinutes(5), adaId, adaSession.Id, fair.Id));
        fixture.Context.BookMovements.Add(AddMovement(
            oldTitle, BookMovementType.DirectEntry, adaSession.StartedAt.AddMinutes(15), adaId, adaSession.Id, fair.Id));
        fixture.Context.BookMovements.Add(AddMovement(
            youth, BookMovementType.DirectEntry, beaSession.StartedAt.AddMinutes(5), beaId, beaSession.Id, fair.Id));

        var adaSale = AddMovement(
            roman,
            BookMovementType.Sale,
            new DateTime(2026, 9, 10, 10, 0, 0, DateTimeKind.Utc),
            adaId,
            null,
            fair.Id,
            quantity: -2);
        fixture.Context.BookMovements.Add(adaSale);
        fixture.Context.BookMovements.Add(AddMovement(
            roman,
            BookMovementType.Correction,
            new DateTime(2026, 9, 10, 10, 10, 0, DateTimeKind.Utc),
            adaId,
            null,
            fair.Id,
            quantity: 1,
            reversalOfMovementId: adaSale.Id));
        fixture.Context.BookMovements.Add(AddMovement(
            youth,
            BookMovementType.Sale,
            new DateTime(2026, 9, 10, 10, 20, 0, DateTimeKind.Utc),
            beaId,
            null,
            fair.Id,
            quantity: -1));
        await fixture.Context.SaveChangesAsync();

        var clock = Substitute.For<IDateTimeProvider>();
        clock.UtcNow.Returns(GeneratedAt);
        var handler = new GetAdminVolunteerStatisticsQueryHandler(fixture.Context, clock);

        var result = await handler.Handle(
            new GetAdminVolunteerStatisticsQuery(null, null, fair.Id.Value),
            CancellationToken.None);

        result.IsError.Should().BeFalse();
        result.Value.Team.ActiveVolunteerCount.Should().Be(2);
        result.Value.Team.ScannedCount.Should().Be(4);
        result.Value.Team.KeptCount.Should().Be(3);
        result.Value.Team.SoldQuantity.Should().Be(2);
        result.Value.Team.ScanDurationMinutes.Should().Be(180);
        result.Value.Team.CashDurationMinutes.Should().Be(30);
        var ada = result.Value.Volunteers.Single(volunteer => volunteer.DisplayName == "Ada Lovelace");
        ada.SoldQuantity.Should().Be(1);
        ada.WaitingQuantity.Should().Be(1);
        ada.Roles.Should().Equal("Tri", "Caisse");
        result.Value.Volunteers.Should().ContainSingle(volunteer =>
            volunteer.DisplayName == "Bea Booker" && volunteer.SoldQuantity == 1);
        result.Value.MonthlyActivity.Should().HaveCount(2);
        result.Value.Renewal.NewCount.Should().Be(2);
    }

    [Fact]
    public async Task Handle_rejects_an_invalid_period()
    {
        await using var fixture = await ScanBookFixture.CreateAsync();
        var clock = Substitute.For<IDateTimeProvider>();
        clock.UtcNow.Returns(GeneratedAt);
        var handler = new GetAdminVolunteerStatisticsQueryHandler(fixture.Context, clock);

        var result = await handler.Handle(
            new GetAdminVolunteerStatisticsQuery(
                new DateTimeOffset(GeneratedAt),
                new DateTimeOffset(GeneratedAt.AddDays(-1)),
                null),
            CancellationToken.None);

        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("Book.InvalidPeriod");
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
        return BookMovement.Create(
            BookMovementId.CreateUnique(),
            book.Id,
            type,
            quantity,
            occurredAt,
            occurredAt.AddMinutes(1),
            false,
            scanSessionId,
            volunteerId,
            fairId,
            null,
            Guid.NewGuid(),
            reversalOfMovementId);
    }
}
