using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using Vole_Papillon_Damour.Application.Common.Interfaces.Persistence;
using Vole_Papillon_Damour.Application.ManualRequests.Commands.Cancel;
using Vole_Papillon_Damour.Application.ManualRequests.Commands.MarkGiven;
using Vole_Papillon_Damour.Application.ManualRequests.Commands.Refuse;
using Vole_Papillon_Damour.Domain.BookMovementAggregate.ValueObjects;
using Vole_Papillon_Damour.Domain.Common.Errors;
using Vole_Papillon_Damour.Domain.ManualRequestAggregate;
using Vole_Papillon_Damour.Domain.ManualRequestAggregate.ValueObjects;
using Vole_Papillon_Damour.Domain.ScanSessionAggregate.ValueObjects;

namespace Vole_Papillon_Damour.Application.tests.ManualRequests.Commands;

public sealed class DecisionCommandHandlerTests
{
    private const string Isbn = "9782070612758";

    [Fact]
    public async Task Given_ClosesWithoutStockOrSale()
    {
        await using var fixture = await ManualRequestFixture.CreateAsync();
        var request = await CreateFoundRequestAsync(fixture, quantityAvailable: 0);
        var handler = new MarkManualRequestGivenCommandHandler(fixture.Context, fixture.CreateClock());

        var result = await handler.Handle(
            new MarkManualRequestGivenCommand(request.Id, ManualRequestFixture.AdministratorId),
            CancellationToken.None);

        result.IsError.Should().BeFalse();
        var book = await fixture.Context.Books.SingleAsync();
        book.QuantityAvailable.Should().Be(0);
        book.SalesCount.Should().Be(0);
        (await fixture.Context.BookMovements.CountAsync()).Should().Be(0);
        var savedRequest = await fixture.Context.ManualBookRequests.SingleAsync();
        savedRequest.Status.Should().Be(ManualBookRequestStatus.Given);
        savedRequest.ReleaseMovementId.Should().BeNull();
    }

    [Fact]
    public async Task Given_OnSearching_ReturnsInvalidTransition()
    {
        await using var fixture = await ManualRequestFixture.CreateAsync();
        var requester = await fixture.AddRequesterAsync();
        var request = await fixture.AddEditionRequestAsync(requester.Id, Isbn);
        var handler = new MarkManualRequestGivenCommandHandler(fixture.Context, fixture.CreateClock());

        var result = await handler.Handle(
            new MarkManualRequestGivenCommand(request.Id, ManualRequestFixture.AdministratorId),
            CancellationToken.None);

        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be(Errors.ManualRequest.InvalidTransition().Code);
        (await fixture.Context.ManualBookRequests.SingleAsync()).Status.Should().Be(ManualBookRequestStatus.Searching);
    }

    [Fact]
    public async Task Refuse_Close_ReleasesAndQueuesImmediateAlerts()
    {
        await using var fixture = await ManualRequestFixture.CreateAsync();
        var request = await CreateFoundRequestAsync(fixture, quantityAvailable: 0);
        var outbox = CreateOutbox(notifiedMembers: 2);
        var handler = new RefuseManualRequestCommandHandler(fixture.Context, fixture.CreateClock(), outbox);

        var result = await handler.Handle(
            new RefuseManualRequestCommand(request.Id, KeepSearching: false, ManualRequestFixture.AdministratorId),
            CancellationToken.None);

        result.IsError.Should().BeFalse();
        result.Value.NotifiedMemberCount.Should().Be(2);
        (await fixture.Context.Books.SingleAsync()).QuantityAvailable.Should().Be(1);
        var savedRequest = await fixture.Context.ManualBookRequests.SingleAsync();
        savedRequest.Status.Should().Be(ManualBookRequestStatus.Refused);
        savedRequest.ReleaseMovementId.Should().NotBeNull();
        var movement = await fixture.Context.BookMovements.SingleAsync();
        movement.Id.Should().Be(savedRequest.ReleaseMovementId);
        movement.Type.Should().Be(BookMovementType.DirectEntry);
        await outbox.Received(1).QueueForReleaseAsync(
            movement.Id,
            ManualRequestFixture.DefaultNow,
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Refuse_KeepSearching_ReturnsToSearchingAndPreservesRequestAge()
    {
        await using var fixture = await ManualRequestFixture.CreateAsync();
        var requestedAt = ManualRequestFixture.DefaultNow.AddDays(-2);
        var request = await CreateFoundRequestAsync(fixture, quantityAvailable: 0, requestedAt);
        var outbox = CreateOutbox(notifiedMembers: 1);
        var handler = new RefuseManualRequestCommandHandler(fixture.Context, fixture.CreateClock(), outbox);

        var result = await handler.Handle(
            new RefuseManualRequestCommand(request.Id, KeepSearching: true, ManualRequestFixture.AdministratorId),
            CancellationToken.None);

        result.IsError.Should().BeFalse();
        result.Value.NotifiedMemberCount.Should().Be(0);
        var savedRequest = await fixture.Context.ManualBookRequests.SingleAsync();
        savedRequest.Status.Should().Be(ManualBookRequestStatus.Searching);
        savedRequest.RequestedAt.Should().Be(requestedAt);
        savedRequest.FoundIsbn13.Should().BeNull();
        savedRequest.HoldMovementId.Should().BeNull();
        (await fixture.Context.Books.SingleAsync()).QuantityAvailable.Should().Be(0);
        (await fixture.Context.BookMovements.CountAsync()).Should().Be(0);
        await outbox.DidNotReceive().QueueForReleaseAsync(
            Arg.Any<BookMovementId>(), Arg.Any<DateTime>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Cancel_Searching_NoRelease()
    {
        await using var fixture = await ManualRequestFixture.CreateAsync();
        var requester = await fixture.AddRequesterAsync();
        var request = await fixture.AddEditionRequestAsync(requester.Id, Isbn);
        var outbox = CreateOutbox(notifiedMembers: 1);
        var handler = new CancelManualRequestCommandHandler(fixture.Context, fixture.CreateClock(), outbox);

        var result = await handler.Handle(
            new CancelManualRequestCommand(request.Id, ManualRequestFixture.AdministratorId),
            CancellationToken.None);

        result.IsError.Should().BeFalse();
        result.Value.NotifiedMemberCount.Should().Be(0);
        (await fixture.Context.ManualBookRequests.SingleAsync()).Status.Should().Be(ManualBookRequestStatus.Cancelled);
        (await fixture.Context.BookMovements.CountAsync()).Should().Be(0);
        await outbox.DidNotReceive().QueueForReleaseAsync(
            Arg.Any<BookMovementId>(), Arg.Any<DateTime>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Cancel_Found_ReleasesLikeRefusal()
    {
        await using var fixture = await ManualRequestFixture.CreateAsync();
        var request = await CreateFoundRequestAsync(fixture, quantityAvailable: 0);
        var outbox = CreateOutbox(notifiedMembers: 3);
        var handler = new CancelManualRequestCommandHandler(fixture.Context, fixture.CreateClock(), outbox);

        var result = await handler.Handle(
            new CancelManualRequestCommand(request.Id, ManualRequestFixture.AdministratorId),
            CancellationToken.None);

        result.IsError.Should().BeFalse();
        result.Value.NotifiedMemberCount.Should().Be(3);
        (await fixture.Context.Books.SingleAsync()).QuantityAvailable.Should().Be(1);
        var savedRequest = await fixture.Context.ManualBookRequests.SingleAsync();
        savedRequest.Status.Should().Be(ManualBookRequestStatus.Cancelled);
        savedRequest.ReleaseMovementId.Should().NotBeNull();
        (await fixture.Context.BookMovements.SingleAsync()).Type.Should().Be(BookMovementType.DirectEntry);
        await outbox.Received(1).QueueForReleaseAsync(
            savedRequest.ReleaseMovementId!,
            ManualRequestFixture.DefaultNow,
            Arg.Any<CancellationToken>());
    }

    private static async Task<ManualBookRequest> CreateFoundRequestAsync(
        ManualRequestFixture fixture,
        int quantityAvailable,
        DateTime? requestedAt = null)
    {
        var requester = await fixture.AddRequesterAsync();
        var book = await fixture.AddBookAsync(Isbn, quantityAvailable);
        var session = await fixture.AddSessionAsync(ScanMode.AvailableNow);
        var request = await fixture.AddEditionRequestAsync(requester.Id, Isbn, requestedAt);
        request.MarkFoundByScan(
            book.Id,
            session.Id,
            BookMovementId.CreateUnique(),
            ManualRequestFixture.VolunteerId,
            ManualRequestFixture.DefaultNow);
        await fixture.Context.SaveChangesAsync();
        return request;
    }

    private static IBookAlertOutbox CreateOutbox(int notifiedMembers)
    {
        var outbox = Substitute.For<IBookAlertOutbox>();
        outbox.QueueForReleaseAsync(
                Arg.Any<BookMovementId>(), Arg.Any<DateTime>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(notifiedMembers));
        return outbox;
    }
}
