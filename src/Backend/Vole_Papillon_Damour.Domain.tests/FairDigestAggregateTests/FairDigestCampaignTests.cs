using FluentAssertions;
using Vole_Papillon_Damour.Domain.EventsAggregate.ValueObjects;
using Vole_Papillon_Damour.Domain.FairDigestAggregate;

namespace Vole_Papillon_Damour.Domain.tests.FairDigestAggregateTests;

public sealed class FairDigestCampaignTests
{
    [Fact]
    public void Resume_AfterStart_IsRefused()
    {
        var at = new DateTime(2026, 11, 14, 9, 0, 0, DateTimeKind.Utc);
        var campaign = FairDigestCampaign.Create(
            AssoEventsId.Create(Guid.Parse("74df8b47-56be-4c65-9f66-8f5ab32d3268")),
            at);
        campaign.Start(at.AddMinutes(1));
        var act = () => campaign.Resume(at.AddMinutes(2));

        act.Should().Throw<InvalidOperationException>();
    }
}
