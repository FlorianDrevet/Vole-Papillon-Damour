using System.Diagnostics;
using FluentAssertions;
using Vole_Papillon_Damour.Application.Books.Commands.ScanBook;
using Vole_Papillon_Damour.Application.Common.Observability;
using Vole_Papillon_Damour.Domain.ScanSessionAggregate.ValueObjects;

namespace Vole_Papillon_Damour.Application.tests.Books.Commands.ScanBook;

public sealed class ScanBookObservabilityTests
{
    [Fact]
    public async Task Handle_WhenScanIsPersisted_EmitsGestureCorrelatedPersistenceActivity()
    {
        var stoppedActivities = new List<Activity>();
        using var activityListener = new ActivityListener
        {
            ShouldListenTo = source => source.Name == BookScanTelemetry.ActivitySourceName,
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded,
            ActivityStopped = activity => stoppedActivities.Add(activity),
        };
        ActivitySource.AddActivityListener(activityListener);

        await using var fixture = await ScanBookFixture.CreateAsync();
        var session = await fixture.AddSessionAsync(ScanMode.AvailableNow);
        var gestureId = Guid.Parse("00000000-0000-0000-0000-000000000042");
        var command = new ScanBookCommand(
            session.Id,
            "9782070363735",
            Kept: true,
            OccurredAt: ScanBookCommandHandlerTests.ClientScanAt,
            ClientGestureId: gestureId);

        var result = await fixture.CreateHandler().Handle(command, CancellationToken.None);

        result.IsError.Should().BeFalse();
        var activity = stoppedActivities
            .Should()
            .ContainSingle(candidate =>
                candidate.OperationName == BookScanTelemetry.ScanPersistenceActivityName &&
                object.Equals(
                    candidate.GetTagItem(BookScanTelemetry.ScanClientGestureIdTagName),
                    gestureId.ToString()))
            .Subject;
        activity.GetTagItem(BookScanTelemetry.ScanClientGestureIdTagName)
            .Should()
            .Be(gestureId.ToString());
        activity.GetTagItem(BookScanTelemetry.ScanPersistenceOutcomeTagName)
            .Should()
            .Be(BookScanTelemetry.PersistedOutcome);
    }
}
