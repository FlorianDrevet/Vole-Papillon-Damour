using FluentAssertions;
using NSubstitute;
using Vole_Papillon_Damour.Application.AccountDeletion;
using Vole_Papillon_Damour.Application.Common.Interfaces.Persistence;
using Vole_Papillon_Damour.Application.Common.Interfaces.Services;
using Vole_Papillon_Damour.Application.Common.Models;

namespace Vole_Papillon_Damour.Application.tests.AccountDeletion;

public class AccountDeletionServiceTests
{
    private static readonly DateTime Now = new(2026, 9, 3, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task RequestAsync_WhenGraphDeletionSucceeds_FinalizesTheLocalProjection()
    {
        var store = Substitute.For<IAccountDeletionStore>();
        var directory = Substitute.For<IEntraUserDirectory>();
        var clock = Substitute.For<IDateTimeProvider>();
        var workItem = new AccountDeletionWorkItem(Guid.NewGuid(), Guid.NewGuid(), "entra-object-id");

        clock.UtcNow.Returns(Now);
        store.EnsurePendingAsync("entra-object-id", Now, Arg.Any<CancellationToken>()).Returns(workItem);

        var service = new AccountDeletionService(store, directory, clock);

        var result = await service.RequestAsync("entra-object-id", CancellationToken.None);

        result.IsCompleted.Should().BeTrue();
        Received.InOrder(async () =>
        {
            await directory.DeleteAsync("entra-object-id", Arg.Any<CancellationToken>());
            await store.FinalizeAsync(workItem, Now, Arg.Any<CancellationToken>());
        });
        await store.DidNotReceive().RecordFailureAsync(
            Arg.Any<Guid>(),
            Arg.Any<string>(),
            Arg.Any<DateTime>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RequestAsync_WhenGraphDeletionFails_RecordsFailureAndLeavesTheRequestQueued()
    {
        var store = Substitute.For<IAccountDeletionStore>();
        var directory = Substitute.For<IEntraUserDirectory>();
        var clock = Substitute.For<IDateTimeProvider>();
        var workItem = new AccountDeletionWorkItem(Guid.NewGuid(), Guid.NewGuid(), "entra-object-id");

        clock.UtcNow.Returns(Now);
        store.EnsurePendingAsync("entra-object-id", Now, Arg.Any<CancellationToken>()).Returns(workItem);
        directory.DeleteAsync("entra-object-id", Arg.Any<CancellationToken>())
            .Returns(Task.FromException(new AccountDeletionDependencyException("graph-http-503")));

        var service = new AccountDeletionService(store, directory, clock);

        var result = await service.RequestAsync("entra-object-id", CancellationToken.None);

        result.IsCompleted.Should().BeFalse();
        await store.Received(1).RecordFailureAsync(
            workItem.RequestId,
            "graph-http-503",
            Now,
            Arg.Any<CancellationToken>());
        await store.DidNotReceive().FinalizeAsync(
            Arg.Any<AccountDeletionWorkItem>(),
            Arg.Any<DateTime>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ProcessPendingAsync_ClaimsAndProcessesTheWholeBatch()
    {
        var store = Substitute.For<IAccountDeletionStore>();
        var directory = Substitute.For<IEntraUserDirectory>();
        var clock = Substitute.For<IDateTimeProvider>();
        var first = new AccountDeletionWorkItem(Guid.NewGuid(), Guid.NewGuid(), "first-object-id");
        var second = new AccountDeletionWorkItem(Guid.NewGuid(), null, "second-object-id");

        clock.UtcNow.Returns(Now);
        store.ClaimPendingAsync(Now, TimeSpan.FromMinutes(5), 50, Arg.Any<CancellationToken>())
            .Returns(new[] { first, second });

        var service = new AccountDeletionService(store, directory, clock);

        var processedCount = await service.ProcessPendingAsync(CancellationToken.None);

        processedCount.Should().Be(2);
        await store.Received(1).ClaimPendingAsync(
            Now,
            TimeSpan.FromMinutes(5),
            50,
            Arg.Any<CancellationToken>());
        await directory.Received(1).DeleteAsync("first-object-id", Arg.Any<CancellationToken>());
        await directory.Received(1).DeleteAsync("second-object-id", Arg.Any<CancellationToken>());
        await store.Received(1).FinalizeAsync(first, Now, Arg.Any<CancellationToken>());
        await store.Received(1).FinalizeAsync(second, Now, Arg.Any<CancellationToken>());
    }
}
