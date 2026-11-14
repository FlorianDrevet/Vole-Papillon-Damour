using FluentAssertions;
using Vole_Papillon_Damour.Domain.FairDigestAggregate;
using Vole_Papillon_Damour.Domain.UserAggregate.ValueObjects;

namespace Vole_Papillon_Damour.Domain.tests.FairDigestAggregateTests;

public sealed class FairDigestDeliveryTests
{
    private static readonly DateTime SentAt = new(2026, 11, 14, 9, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void MarkSent_RecordsTheUtcSendTime()
    {
        var delivery = FairDigestDelivery.Create(
            Guid.Parse("d3d813c6-376b-49ee-854e-2a2f303e9929"),
            UserId.Create(Guid.Parse("7f0b0f0e-0000-0000-0000-00000000c0de")));

        delivery.MarkSent(SentAt);

        delivery.Status.Should().Be(FairDigestDeliveryStatus.Sent);
        delivery.SentAt.Should().Be(SentAt);
        delivery.Error.Should().BeNull();
    }

    [Fact]
    public void MarkSkippedAndFailed_RecordTerminalOutcomes()
    {
        var skipped = NewDelivery();
        var failed = NewDelivery();

        skipped.MarkSkippedNoContent();
        failed.MarkFailed("EmailException");

        skipped.Status.Should().Be(FairDigestDeliveryStatus.SkippedNoContent);
        failed.Status.Should().Be(FairDigestDeliveryStatus.Failed);
        failed.Error.Should().Be("EmailException");
    }

    private static FairDigestDelivery NewDelivery() => FairDigestDelivery.Create(
        Guid.NewGuid(),
        UserId.Create(Guid.NewGuid()));
}
