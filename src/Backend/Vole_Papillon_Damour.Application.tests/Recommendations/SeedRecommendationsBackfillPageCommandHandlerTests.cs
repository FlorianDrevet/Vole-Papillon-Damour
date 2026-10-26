using FluentAssertions;
using NSubstitute;
using Vole_Papillon_Damour.Application.Common.Interfaces.Services;
using Vole_Papillon_Damour.Application.Recommendations.Commands.SeedRecommendationsBackfillPage;
using Vole_Papillon_Damour.Domain.RecommendationAggregate;

namespace Vole_Papillon_Damour.Application.tests.Recommendations;

public sealed class SeedRecommendationsBackfillPageCommandHandlerTests
{
    private static readonly DateTime Now = new(2026, 9, 25, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task Handle_WhenBackfillContainsFailedJob_QueuesRetryAndLeavesReadyJobsAlone()
    {
        await using var fixture = await RecommendationProfileFixture.CreateAsync();
        const string failedIsbn13 = "9783140464079";
        const string readyIsbn13 = "9782070363735";
        await fixture.AddBookAsync(failedIsbn13, Now);
        await fixture.AddBookAsync(readyIsbn13, Now);

        var failedJob = BookRecommendationJob.Create(failedIsbn13);
        failedJob.Request(Now);
        failedJob.TryStart(failedJob.RequestVersion, Now);
        failedJob.Fail(failedJob.RequestVersion, BookRecommendationJobStage.Notice, "previous failure", Now);
        var readyJob = BookRecommendationJob.Create(readyIsbn13);
        readyJob.Request(Now);
        readyJob.TryStart(readyJob.RequestVersion, Now);
        readyJob.Complete(readyJob.RequestVersion, BookRecommendationJobStatus.Ready, Now);
        fixture.Context.BookRecommendationJobs.AddRange(failedJob, readyJob);
        await fixture.Context.SaveChangesAsync();

        var queue = Substitute.For<IBookRecommendationWorkQueue>();
        queue.EnqueueBooksAsync(
                Arg.Any<IReadOnlyCollection<BookRecommendationQueueItem>>(),
                Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);
        var clock = Substitute.For<IDateTimeProvider>();
        clock.UtcNow.Returns(Now);
        var handler = new SeedRecommendationsBackfillPageCommandHandler(fixture.Context, queue, clock);

        var result = await handler.Handle(
            new SeedRecommendationsBackfillPageCommand(
                Guid.Parse("af6b6f41-8fd3-4c4e-b7bf-fda5c8f15930"),
                "9782070363735"),
            CancellationToken.None);

        result.BooksScanned.Should().Be(1);
        result.BooksQueued.Should().Be(1);
        failedJob.Status.Should().Be(BookRecommendationJobStatus.Pending);
        failedJob.RequestVersion.Should().Be(2);
        failedJob.LastError.Should().BeNull();
        readyJob.Status.Should().Be(BookRecommendationJobStatus.Ready);
        await queue.Received(1).EnqueueBooksAsync(
            Arg.Is<IReadOnlyCollection<BookRecommendationQueueItem>>(items =>
                items.Count == 1 && items.Single() == new BookRecommendationQueueItem(failedIsbn13, 2)),
            Arg.Any<CancellationToken>());
    }
}
