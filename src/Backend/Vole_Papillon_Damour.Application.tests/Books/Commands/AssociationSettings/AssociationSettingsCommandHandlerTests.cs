using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Vole_Papillon_Damour.Application.Books.Commands.AssociationSettings;
using Vole_Papillon_Damour.Application.Books.Queries.GetAssociationSettings;
using Vole_Papillon_Damour.Application.tests.Books.Commands.ScanBook;
using Vole_Papillon_Damour.Domain.UserAggregate.ValueObjects;

namespace Vole_Papillon_Damour.Application.tests.Books.Commands.AssociationSettings;

public sealed class AssociationSettingsCommandHandlerTests
{
    [Fact]
    public async Task Update_WhenSettingsDoNotExist_CreatesTheSingletonWithTypedValues()
    {
        await using var fixture = await ScanBookFixture.CreateAsync();
        var updatedBy = UserId.Create(Guid.Parse("00000000-0000-0000-0000-000000000002"));
        var handler = fixture.CreateUpdateAssociationSettingsHandler();

        var result = await handler.Handle(
            new UpdateAssociationSettingsCommand(
                7,
                3,
                45,
                2,
                200,
                60,
                90,
                30,
                updatedBy),
            CancellationToken.None);

        result.IsError.Should().BeFalse();
        result.Value.DuplicateThreshold.Should().Be(7);
        result.Value.DemandSalesThreshold.Should().Be(3);
        result.Value.DeadStockMinAgeDays.Should().Be(45);
        result.Value.DeadStockMinQuantity.Should().Be(2);
        result.Value.WatchlistMaxItems.Should().Be(200);
        result.Value.AlertCooldownDays.Should().Be(60);
        result.Value.SessionIdleTimeoutMinutes.Should().Be(90);
        result.Value.AlertDelayMinutes.Should().Be(30);
        result.Value.UpdatedBy.Should().Be(updatedBy);
        (await fixture.Context.AssociationSettings.CountAsync()).Should().Be(1);
    }

    [Fact]
    public async Task Get_WhenSettingsExist_ReturnsThePersistedSingleton()
    {
        await using var fixture = await ScanBookFixture.CreateAsync();
        var updatedBy = UserId.Create(Guid.Parse("00000000-0000-0000-0000-000000000002"));
        var updateHandler = fixture.CreateUpdateAssociationSettingsHandler();
        await updateHandler.Handle(
            new UpdateAssociationSettingsCommand(
                8,
                4,
                60,
                3,
                150,
                14,
                120,
                180,
                updatedBy),
            CancellationToken.None);

        var result = await fixture.CreateGetAssociationSettingsHandler()
            .Handle(new GetAssociationSettingsQuery(), CancellationToken.None);

        result.IsError.Should().BeFalse();
        result.Value.DuplicateThreshold.Should().Be(8);
        result.Value.DemandSalesThreshold.Should().Be(4);
        result.Value.WatchlistMaxItems.Should().Be(150);
        result.Value.AlertDelayMinutes.Should().Be(180);
        result.Value.UpdatedBy.Should().Be(updatedBy);
    }

    [Fact]
    public async Task Update_WhenThresholdIsZero_ReturnsValidationError()
    {
        await using var fixture = await ScanBookFixture.CreateAsync();
        var handler = fixture.CreateUpdateAssociationSettingsHandler();

        var result = await handler.Handle(
            new UpdateAssociationSettingsCommand(
                0,
                1,
                30,
                1,
                100,
                30,
                120,
                120,
                UserId.Create(Guid.Parse("00000000-0000-0000-0000-000000000002"))),
            CancellationToken.None);

        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("Book.InvalidAssociationSettings");
        (await fixture.Context.AssociationSettings.CountAsync()).Should().Be(0);
    }
}
