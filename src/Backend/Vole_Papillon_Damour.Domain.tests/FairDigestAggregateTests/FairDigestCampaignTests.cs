using FluentAssertions;
using Vole_Papillon_Damour.Domain.EventsAggregate.ValueObjects;
using Vole_Papillon_Damour.Domain.FairDigestAggregate;
using Vole_Papillon_Damour.Domain.UserAggregate.ValueObjects;

namespace Vole_Papillon_Damour.Domain.tests.FairDigestAggregateTests;

public sealed class FairDigestCampaignTests
{
    private static readonly DateTime Now = new(2027, 3, 8, 9, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Resume_ClearsASuspensionBeforeTheCampaignStarts()
    {
        var campaign = NewCampaign();
        campaign.Suspend(AdministratorId(), Now);

        var changed = campaign.Resume(Now.AddMinutes(5));

        changed.Should().BeTrue();
        campaign.SuspendedAt.Should().BeNull();
        campaign.SuspendedBy.Should().BeNull();
    }

    [Fact]
    public void Resume_RejectsACampaignThatHasStarted()
    {
        var campaign = NewCampaign();
        campaign.Start(Now);

        var act = () => campaign.Resume(Now.AddMinutes(5));

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Suspend_RejectsACampaignThatHasStarted()
    {
        var campaign = NewCampaign();
        campaign.Start(Now);

        var act = () => campaign.Suspend(AdministratorId(), Now.AddMinutes(5));

        act.Should().Throw<InvalidOperationException>();
    }

    private static FairDigestCampaign NewCampaign() => FairDigestCampaign.Create(
        AssoEventsId.Create(Guid.Parse("a83c8586-6c92-40a4-a3ab-0f74515b20d7")),
        Now);

    private static UserId AdministratorId() =>
        UserId.Create(Guid.Parse("7f0b0f0e-0000-0000-0000-00000000c0de"));
}
