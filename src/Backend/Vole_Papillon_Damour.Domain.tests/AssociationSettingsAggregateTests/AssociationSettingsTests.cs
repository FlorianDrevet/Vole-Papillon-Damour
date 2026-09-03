using FluentAssertions;
using Vole_Papillon_Damour.Domain.AssociationSettingsAggregate;
using Vole_Papillon_Damour.Domain.UserAggregate.ValueObjects;

namespace Vole_Papillon_Damour.Domain.tests.AssociationSettingsAggregateTests;

public sealed class AssociationSettingsTests
{
    [Fact]
    public void Create_UsesTheDocumentedDefaultsAndSingletonId()
    {
        var settings = AssociationSettings.Create(
            UserId.Create(Guid.Parse("11111111-1111-1111-1111-111111111111")),
            UtcNow());

        settings.Id.Should().Be(AssociationSettings.SingletonId);
        settings.DuplicateThreshold.Should().Be(5);
        settings.DemandSalesThreshold.Should().Be(1);
        settings.WatchlistMaxItems.Should().Be(100);
        settings.AlertCooldownDays.Should().Be(30);
        settings.SessionIdleTimeoutMinutes.Should().Be(120);
        settings.AlertDelayMinutes.Should().Be(120);
    }

    [Fact]
    public void Update_WithNonPositiveSessionTimeout_Throws()
    {
        var settings = AssociationSettings.Create(
            UserId.Create(Guid.Parse("11111111-1111-1111-1111-111111111111")),
            UtcNow());

        var action = () => settings.Update(
            duplicateThreshold: 5,
            demandSalesThreshold: 1,
            deadStockMinAgeDays: 30,
            deadStockMinQuantity: 1,
            watchlistMaxItems: 100,
            alertCooldownDays: 30,
            sessionIdleTimeoutMinutes: 0,
            alertDelayMinutes: 120,
            updatedBy: settings.UpdatedBy,
            updatedAt: UtcNow().AddMinutes(1));

        action.Should().Throw<ArgumentOutOfRangeException>();
    }

    private static DateTime UtcNow() => new(2026, 9, 3, 17, 0, 0, DateTimeKind.Utc);
}
