using FluentAssertions;
using NSubstitute;
using Vole_Papillon_Damour.Application.Common.Interfaces.Persistence;
using Vole_Papillon_Damour.Application.Common.Interfaces.Services;
using Vole_Papillon_Damour.Application.Common.Models;
using Vole_Papillon_Damour.Application.Common.Services;
using Vole_Papillon_Damour.Domain.BookAggregate.ValueObjects;
using Vole_Papillon_Damour.Domain.ScanSessionAggregate.ValueObjects;

namespace Vole_Papillon_Damour.Application.tests.Common;

public sealed class BookAlertDeliveryServiceTests
{
    private static readonly DateTime Now =
        new(2026, 9, 3, 20, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task ProcessPending_WhenEmailIsDisabled_DoesNotClaimMessages()
    {
        var outbox = Substitute.For<IBookAlertOutbox>();
        var sender = Substitute.For<IBookAlertEmailSender>();
        sender.IsEnabled.Returns(false);
        var clock = Substitute.For<IDateTimeProvider>();
        clock.UtcNow.Returns(Now);
        var service = new BookAlertDeliveryService(outbox, sender, clock);

        var result = await service.ProcessPendingAsync(CancellationToken.None);

        result.Disabled.Should().BeTrue();
        result.ClaimedCount.Should().Be(0);
        await outbox.Received(1).GetOldestDueAtAsync(Now, Arg.Any<CancellationToken>());
        await outbox.DidNotReceiveWithAnyArgs().ClaimDueAsync(default, default, default, default);
    }

    [Fact]
    public async Task ProcessPending_WhenDeliverySucceeds_MarksTheMessageSentWithItsItems()
    {
        var outbox = Substitute.For<IBookAlertOutbox>();
        var sender = Substitute.For<IBookAlertEmailSender>();
        sender.IsEnabled.Returns(true);
        var clock = Substitute.For<IDateTimeProvider>();
        clock.UtcNow.Returns(Now);
        var isbn = ParseIsbn("9782070363735");
        var item = new BookAlertOutboxItem(
            isbn,
            "work-42",
            "Titre",
            "Auteur",
            1,
            ScanMode.AvailableNow,
            null);
        var workItem = new BookAlertOutboxWorkItem(
            Guid.NewGuid(),
            Guid.NewGuid(),
            [item],
            Attempts: 1,
            ClaimedUntil: Now.AddMinutes(5));
        var delivery = new BookAlertDelivery(
            workItem.MessageId,
            workItem.MemberId,
            "member@example.org",
            "Prénom Nom",
            [item]);
        outbox.ClaimDueAsync(Now, TimeSpan.FromMinutes(5), 50, Arg.Any<CancellationToken>())
            .Returns(new[] { workItem });
        outbox.GetPendingDeliveryAsync(
                workItem.MessageId,
                workItem.ClaimedUntil,
                Now,
                Arg.Any<CancellationToken>())
            .Returns(delivery);
        outbox.MarkSentAsync(
                workItem.MessageId,
                workItem.ClaimedUntil,
                Now,
                Arg.Is<IReadOnlyCollection<Isbn13>>(items => items.Single() == isbn),
                Arg.Any<CancellationToken>())
            .Returns(true);

        var service = new BookAlertDeliveryService(outbox, sender, clock);

        var result = await service.ProcessPendingAsync(CancellationToken.None);

        result.SentCount.Should().Be(1);
        result.FailedCount.Should().Be(0);
        await sender.Received(1).SendAsync(delivery, Arg.Any<CancellationToken>());
        await outbox.Received(1).MarkSentAsync(
            workItem.MessageId,
            workItem.ClaimedUntil,
            Now,
            Arg.Any<IReadOnlyCollection<Isbn13>>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ProcessPending_WhenSenderFails_RecordsAReplayableFailure()
    {
        var outbox = Substitute.For<IBookAlertOutbox>();
        var sender = Substitute.For<IBookAlertEmailSender>();
        sender.IsEnabled.Returns(true);
        var clock = Substitute.For<IDateTimeProvider>();
        clock.UtcNow.Returns(Now);
        var workItem = new BookAlertOutboxWorkItem(
            Guid.NewGuid(),
            Guid.NewGuid(),
            [],
            Attempts: 1,
            ClaimedUntil: Now.AddMinutes(5));
        var delivery = new BookAlertDelivery(
            workItem.MessageId,
            workItem.MemberId,
            "member@example.org",
            null,
            [new BookAlertOutboxItem(
                ParseIsbn("9782070363735"),
                null,
                "Titre",
                null,
                1,
                ScanMode.AvailableNow,
                null)]);
        outbox.ClaimDueAsync(Now, TimeSpan.FromMinutes(5), 50, Arg.Any<CancellationToken>())
            .Returns(new[] { workItem });
        outbox.GetPendingDeliveryAsync(
                workItem.MessageId,
                workItem.ClaimedUntil,
                Now,
                Arg.Any<CancellationToken>())
            .Returns(delivery);
        sender.SendAsync(delivery, Arg.Any<CancellationToken>())
            .Returns(Task.FromException(new InvalidOperationException("ACS unavailable")));

        var service = new BookAlertDeliveryService(outbox, sender, clock);

        var result = await service.ProcessPendingAsync(CancellationToken.None);

        result.FailedCount.Should().Be(1);
        await outbox.Received(1).RecordFailureAsync(
            workItem.MessageId,
            workItem.ClaimedUntil,
            "InvalidOperationException",
            Now,
            Arg.Any<CancellationToken>());
        await outbox.DidNotReceiveWithAnyArgs().MarkSentAsync(
            default,
            default,
            default,
            default!,
            default);
    }

    [Fact]
    public async Task ProcessPending_RechecksTheLeaseWithTheCurrentClockBeforeDelivery()
    {
        var outbox = Substitute.For<IBookAlertOutbox>();
        var sender = Substitute.For<IBookAlertEmailSender>();
        sender.IsEnabled.Returns(true);
        var clock = Substitute.For<IDateTimeProvider>();
        var afterClaim = Now.AddMinutes(4);
        clock.UtcNow.Returns(Now, afterClaim, afterClaim);
        var isbn = ParseIsbn("9782070363735");
        var item = new BookAlertOutboxItem(
            isbn,
            null,
            "Titre",
            null,
            1,
            ScanMode.AvailableNow,
            null);
        var workItem = new BookAlertOutboxWorkItem(
            Guid.NewGuid(),
            Guid.NewGuid(),
            [item],
            Attempts: 1,
            ClaimedUntil: Now.AddMinutes(5));
        var delivery = new BookAlertDelivery(
            workItem.MessageId,
            workItem.MemberId,
            "member@example.org",
            null,
            [item]);
        outbox.ClaimDueAsync(Now, TimeSpan.FromMinutes(5), 50, Arg.Any<CancellationToken>())
            .Returns(new[] { workItem });
        outbox.GetPendingDeliveryAsync(
                workItem.MessageId,
                workItem.ClaimedUntil,
                afterClaim,
                Arg.Any<CancellationToken>())
            .Returns(delivery);
        outbox.MarkSentAsync(
                workItem.MessageId,
                workItem.ClaimedUntil,
                afterClaim,
                Arg.Is<IReadOnlyCollection<Isbn13>>(items => items.Single() == isbn),
                Arg.Any<CancellationToken>())
            .Returns(true);

        var service = new BookAlertDeliveryService(outbox, sender, clock);

        var result = await service.ProcessPendingAsync(CancellationToken.None);

        result.SentCount.Should().Be(1);
        await outbox.Received(1).GetPendingDeliveryAsync(
            workItem.MessageId,
            workItem.ClaimedUntil,
            afterClaim,
            Arg.Any<CancellationToken>());
    }

    private static Isbn13 ParseIsbn(string value)
    {
        Isbn13.TryCreate(value, out var isbn).Should().BeTrue();
        return isbn;
    }
}
