using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Vole_Papillon_Damour.Application.ManualRequests.Commands.EraseExpired;
using Vole_Papillon_Damour.Domain.BookMovementAggregate.ValueObjects;
using Vole_Papillon_Damour.Domain.ManualRequestAggregate;

namespace Vole_Papillon_Damour.Application.tests.ManualRequests.Commands;

public sealed class EraseExpiredHandlerTests
{
    private static readonly DateTime Now = ManualRequestFixture.DefaultNow;

    [Fact]
    public async Task Erases_requester_91_days_after_last_closure_but_keeps_one_closed_89_days_ago()
    {
        await using var fixture = await ManualRequestFixture.CreateAsync(Now);
        var expired = await AddRequesterAsync(fixture, Now.AddDays(-200));
        var recent = await AddRequesterAsync(fixture, Now.AddDays(-200));
        await AddClosedRequestAsync(fixture, expired, Now.AddDays(-91));
        await AddClosedRequestAsync(fixture, recent, Now.AddDays(-89));
        var handler = new EraseExpiredManualRequestersCommandHandler(fixture.Context, fixture.CreateClock());

        var result = await handler.Handle(new EraseExpiredManualRequestersCommand(), CancellationToken.None);

        result.Should().Be(1);
        (await fixture.Context.ManualRequesters.SingleAsync(requester => requester.Id == expired.Id)).IsErased.Should().BeTrue();
        (await fixture.Context.ManualRequesters.SingleAsync(requester => requester.Id == recent.Id)).IsErased.Should().BeFalse();
    }

    [Fact]
    public async Task Keeps_requester_with_any_open_request_even_when_another_request_is_old_and_closed()
    {
        await using var fixture = await ManualRequestFixture.CreateAsync(Now);
        var requester = await AddRequesterAsync(fixture, Now.AddDays(-200));
        await AddClosedRequestAsync(fixture, requester, Now.AddDays(-120));
        fixture.Context.ManualBookRequests.Add(ManualBookRequest.CreateWork(
            Guid.NewGuid(), requester.Id, ManualRequestFixture.DefaultWorkId,
            "Open book", null, null, null, Now.AddDays(-100)));
        await fixture.Context.SaveChangesAsync();
        var handler = new EraseExpiredManualRequestersCommandHandler(fixture.Context, fixture.CreateClock());

        var result = await handler.Handle(new EraseExpiredManualRequestersCommand(), CancellationToken.None);

        result.Should().Be(0);
        (await fixture.Context.ManualRequesters.SingleAsync()).IsErased.Should().BeFalse();
    }

    [Fact]
    public async Task Uses_requester_creation_time_when_there_are_no_requests()
    {
        await using var fixture = await ManualRequestFixture.CreateAsync(Now);
        var expired = await AddRequesterAsync(fixture, Now.AddDays(-91));
        var recent = await AddRequesterAsync(fixture, Now.AddDays(-89));
        var handler = new EraseExpiredManualRequestersCommandHandler(fixture.Context, fixture.CreateClock());

        var result = await handler.Handle(new EraseExpiredManualRequestersCommand(), CancellationToken.None);

        result.Should().Be(1);
        (await fixture.Context.ManualRequesters.SingleAsync(requester => requester.Id == expired.Id)).IsErased.Should().BeTrue();
        (await fixture.Context.ManualRequesters.SingleAsync(requester => requester.Id == recent.Id)).IsErased.Should().BeFalse();
    }

    [Fact]
    public async Task Ignores_requester_whose_contact_details_are_already_erased()
    {
        await using var fixture = await ManualRequestFixture.CreateAsync(Now);
        await AddRequesterAsync(fixture, Now.AddDays(-200), erasedAt: Now.AddDays(-100));
        var handler = new EraseExpiredManualRequestersCommandHandler(fixture.Context, fixture.CreateClock());

        var result = await handler.Handle(new EraseExpiredManualRequestersCommand(), CancellationToken.None);

        result.Should().Be(0);
        (await fixture.Context.ManualRequesters.SingleAsync()).IsErased.Should().BeTrue();
    }

    private static async Task<ManualRequester> AddRequesterAsync(
        ManualRequestFixture fixture,
        DateTime createdAt,
        DateTime? erasedAt = null)
    {
        var requester = ManualRequester.Create(
            Guid.NewGuid(), "Martin", "Jeanne", "0612345678", true,
            ManualRequestFixture.AdministratorId, createdAt);
        if (erasedAt is { } at)
        {
            requester.Erase(at);
        }

        fixture.Context.ManualRequesters.Add(requester);
        await fixture.Context.SaveChangesAsync();
        return requester;
    }

    private static async Task AddClosedRequestAsync(
        ManualRequestFixture fixture,
        ManualRequester requester,
        DateTime closedAt)
    {
        var request = ManualBookRequest.CreateWork(
            Guid.NewGuid(), requester.Id, ManualRequestFixture.DefaultWorkId,
            "Closed book", null, null, null, closedAt.AddDays(-10));
        request.MarkFoundFromStock(
            ManualRequestFixture.ParseIsbn("9782070612758"),
            pickedFromFairId: null,
            pickedAnnouncementSessionId: null,
            BookMovementId.CreateUnique(),
            ManualRequestFixture.AdministratorId,
            closedAt.AddMinutes(-1));
        request.MarkGiven(ManualRequestFixture.AdministratorId, closedAt);
        fixture.Context.ManualBookRequests.Add(request);
        await fixture.Context.SaveChangesAsync();
    }
}
