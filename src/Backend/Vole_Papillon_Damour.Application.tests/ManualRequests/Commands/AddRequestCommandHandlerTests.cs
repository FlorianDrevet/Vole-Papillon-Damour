using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Vole_Papillon_Damour.Application.ManualRequests.Commands.AddRequest;
using Vole_Papillon_Damour.Application.ManualRequests.Commands.PickFromStock;
using Vole_Papillon_Damour.Domain.AssociationSettingsAggregate;
using Vole_Papillon_Damour.Domain.BookAggregate.ValueObjects;
using Vole_Papillon_Damour.Domain.BookMovementAggregate.ValueObjects;
using Vole_Papillon_Damour.Domain.Common.Errors;
using Vole_Papillon_Damour.Domain.ManualRequestAggregate.ValueObjects;
using Vole_Papillon_Damour.Domain.ScanSessionAggregate.ValueObjects;

namespace Vole_Papillon_Damour.Application.tests.ManualRequests.Commands;

public sealed class AddRequestCommandHandlerTests
{
    private const string Isbn = "9782070612758";

    [Fact]
    public async Task Add_WorkScope_CreatesSearchingRequest()
    {
        await using var fixture = await ManualRequestFixture.CreateAsync();
        var requester = await fixture.AddRequesterAsync();
        var handler = new AddManualBookRequestCommandHandler(fixture.Context, fixture.CreateClock());

        var result = await handler.Handle(
            CreateCommand(requester.Id, ManualBookRequestScope.Work, ManualRequestFixture.DefaultWorkId),
            CancellationToken.None);

        result.IsError.Should().BeFalse();
        result.Value.Status.Should().Be(ManualBookRequestStatus.Searching);
        var request = await fixture.Context.ManualBookRequests.SingleAsync();
        request.Id.Should().Be(result.Value.RequestId);
        request.Scope.Should().Be(ManualBookRequestScope.Work);
        request.WorkId.Should().Be(ManualRequestFixture.DefaultWorkId);
        request.Status.Should().Be(ManualBookRequestStatus.Searching);
    }

    [Fact]
    public async Task Add_EditionScope_RequiresValidIsbn()
    {
        await using var fixture = await ManualRequestFixture.CreateAsync();
        var requester = await fixture.AddRequesterAsync();
        var handler = new AddManualBookRequestCommandHandler(fixture.Context, fixture.CreateClock());
        var command = CreateCommand(requester.Id, ManualBookRequestScope.Edition, isbn: "not-an-isbn");

        var result = await handler.Handle(command, CancellationToken.None);

        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be(Errors.ManualRequest.InvalidTarget().Code);
        (await fixture.Context.ManualBookRequests.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task Add_SameOpenTarget_ReturnsDuplicate()
    {
        await using var fixture = await ManualRequestFixture.CreateAsync();
        var requester = await fixture.AddRequesterAsync();
        await fixture.AddWorkRequestAsync(requester.Id);
        var handler = new AddManualBookRequestCommandHandler(fixture.Context, fixture.CreateClock());

        var result = await handler.Handle(
            CreateCommand(requester.Id, ManualBookRequestScope.Work, ManualRequestFixture.DefaultWorkId),
            CancellationToken.None);

        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be(Errors.ManualRequest.DuplicateRequest().Code);
        (await fixture.Context.ManualBookRequests.CountAsync()).Should().Be(1);
    }

    [Fact]
    public async Task Add_OverWatchlistMaxItems_ReturnsLimitReached()
    {
        await using var fixture = await ManualRequestFixture.CreateAsync();
        var settings = AssociationSettings.Create(ManualRequestFixture.AdministratorId, ManualRequestFixture.DefaultNow);
        settings.Update(5, 1, 30, 1, watchlistMaxItems: 1, 30, 120, 120, 10,
            ManualRequestFixture.AdministratorId, ManualRequestFixture.DefaultNow);
        fixture.Context.AssociationSettings.Add(settings);
        var requester = await fixture.AddRequesterAsync();
        await fixture.AddWorkRequestAsync(requester.Id);
        var handler = new AddManualBookRequestCommandHandler(fixture.Context, fixture.CreateClock());

        var result = await handler.Handle(
            CreateCommand(requester.Id, ManualBookRequestScope.Edition, isbn: Isbn),
            CancellationToken.None);

        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be(Errors.ManualRequest.LimitReached(1).Code);
        (await fixture.Context.ManualBookRequests.CountAsync()).Should().Be(1);
    }

    [Fact]
    public async Task Add_ToErasedRequester_ReturnsRequesterErased()
    {
        await using var fixture = await ManualRequestFixture.CreateAsync();
        var requester = await fixture.AddRequesterAsync();
        requester.Erase(ManualRequestFixture.DefaultNow.AddMinutes(1));
        await fixture.Context.SaveChangesAsync();
        var handler = new AddManualBookRequestCommandHandler(fixture.Context, fixture.CreateClock());

        var result = await handler.Handle(
            CreateCommand(requester.Id, ManualBookRequestScope.Work, ManualRequestFixture.DefaultWorkId),
            CancellationToken.None);

        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be(Errors.ManualRequest.RequesterErased().Code);
        (await fixture.Context.ManualBookRequests.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task Add_WithPickFromStock_WithdrawsOneAvailableAndMarksFound()
    {
        await using var fixture = await ManualRequestFixture.CreateAsync();
        var requester = await fixture.AddRequesterAsync();
        await fixture.AddBookAsync(Isbn, quantityAvailable: 2);
        var handler = new AddManualBookRequestCommandHandler(fixture.Context, fixture.CreateClock());

        var result = await handler.Handle(
            CreateCommand(requester.Id, ManualBookRequestScope.Edition, isbn: Isbn, pickFromStock: true),
            CancellationToken.None);

        result.IsError.Should().BeFalse();
        result.Value.Status.Should().Be(ManualBookRequestStatus.Found);
        (await fixture.Context.Books.SingleAsync()).QuantityAvailable.Should().Be(1);
        var request = await fixture.Context.ManualBookRequests.SingleAsync();
        request.Status.Should().Be(ManualBookRequestStatus.Found);
        request.FoundOrigin.Should().Be(ManualBookRequestFoundOrigin.StockAvailable);
        request.FoundIsbn13.Should().Be(ManualRequestFixture.ParseIsbn(Isbn));
        var movement = await fixture.Context.BookMovements.SingleAsync();
        movement.Type.Should().Be(BookMovementType.Withdrawal);
        movement.Quantity.Should().Be(-1);
        movement.Note.Should().Be("ManualRequest.StockPick");
    }

    [Fact]
    public async Task Pick_FromDatedAnnouncementWhenNothingAvailable()
    {
        await using var fixture = await ManualRequestFixture.CreateAsync();
        var requester = await fixture.AddRequesterAsync();
        await fixture.AddBookAsync(Isbn, quantityAvailable: 0);
        var fair = await fixture.AddFairAsync(new DateTimeOffset(ManualRequestFixture.DefaultNow.AddDays(7)));
        var announcement = await fixture.AddAnnouncementAsync(Isbn, fair.Id, quantity: 2);
        var request = await fixture.AddEditionRequestAsync(requester.Id, Isbn);
        var handler = new PickManualRequestFromStockCommandHandler(fixture.Context, fixture.CreateClock());

        var result = await handler.Handle(
            new PickManualRequestFromStockCommand(request.Id, ManualRequestFixture.AdministratorId),
            CancellationToken.None);

        result.IsError.Should().BeFalse();
        var savedRequest = await fixture.Context.ManualBookRequests.SingleAsync();
        savedRequest.Status.Should().Be(ManualBookRequestStatus.Found);
        savedRequest.FoundOrigin.Should().Be(ManualBookRequestFoundOrigin.StockAnnounced);
        savedRequest.PickedFromFairId.Should().Be(fair.Id);
        savedRequest.FoundScanSessionId.Should().Be(announcement.ScanSessionId);
        (await fixture.Context.BookAnnouncements.SingleAsync()).Quantity.Should().Be(1);
        var movement = await fixture.Context.BookMovements.SingleAsync();
        movement.Type.Should().Be(BookMovementType.Correction);
        movement.Quantity.Should().Be(-1);
        movement.AssoEventsId.Should().Be(fair.Id);
        movement.Note.Should().Be("Announcement.Correction: ManualRequest.StockPick");
    }

    [Fact]
    public async Task Pick_WorkScopePrefersEditionWithTheMostAvailableCopies()
    {
        await using var fixture = await ManualRequestFixture.CreateAsync();
        var requester = await fixture.AddRequesterAsync();
        var fewerCopies = await fixture.AddBookAsync(Isbn, quantityAvailable: 1, workId: ManualRequestFixture.DefaultWorkId);
        var moreCopies = await fixture.AddBookAsync("9782070612765", quantityAvailable: 3, workId: ManualRequestFixture.DefaultWorkId);
        var request = await fixture.AddWorkRequestAsync(requester.Id);
        var handler = new PickManualRequestFromStockCommandHandler(fixture.Context, fixture.CreateClock());

        var result = await handler.Handle(
            new PickManualRequestFromStockCommand(request.Id, ManualRequestFixture.AdministratorId),
            CancellationToken.None);

        result.IsError.Should().BeFalse();
        (await fixture.Context.Books.SingleAsync(candidate => candidate.Id == fewerCopies.Id)).QuantityAvailable.Should().Be(1);
        (await fixture.Context.Books.SingleAsync(candidate => candidate.Id == moreCopies.Id)).QuantityAvailable.Should().Be(2);
        (await fixture.Context.ManualBookRequests.SingleAsync()).FoundIsbn13.Should().Be(moreCopies.Id);
        (await fixture.Context.BookMovements.SingleAsync()).Isbn13.Should().Be(moreCopies.Id);
    }

    [Fact]
    public async Task Pick_WorkScopeUsesAnnouncementWhenNoEditionIsAvailable()
    {
        await using var fixture = await ManualRequestFixture.CreateAsync();
        var requester = await fixture.AddRequesterAsync();
        await fixture.AddBookAsync(Isbn, quantityAvailable: 0, workId: ManualRequestFixture.DefaultWorkId);
        var fair = await fixture.AddFairAsync(new DateTimeOffset(ManualRequestFixture.DefaultNow.AddDays(7)));
        var announcement = await fixture.AddAnnouncementAsync(Isbn, fair.Id, quantity: 1);
        var request = await fixture.AddWorkRequestAsync(requester.Id);
        var handler = new PickManualRequestFromStockCommandHandler(fixture.Context, fixture.CreateClock());

        var result = await handler.Handle(
            new PickManualRequestFromStockCommand(request.Id, ManualRequestFixture.AdministratorId),
            CancellationToken.None);

        result.IsError.Should().BeFalse();
        var savedAnnouncement = await fixture.Context.BookAnnouncements.SingleAsync();
        savedAnnouncement.Status.Should().Be(BookAnnouncementStatus.Cancelled);
        (await fixture.Context.ManualBookRequests.SingleAsync()).FoundIsbn13.Should().Be(ManualRequestFixture.ParseIsbn(Isbn));
        (await fixture.Context.ManualBookRequests.SingleAsync()).PickedFromFairId.Should().Be(fair.Id);
        announcement.Quantity.Should().Be(1);
    }

    [Fact]
    public async Task Pick_WithNothing_ReturnsNothingInStock()
    {
        await using var fixture = await ManualRequestFixture.CreateAsync();
        var requester = await fixture.AddRequesterAsync();
        var request = await fixture.AddEditionRequestAsync(requester.Id, Isbn);
        var handler = new PickManualRequestFromStockCommandHandler(fixture.Context, fixture.CreateClock());

        var result = await handler.Handle(
            new PickManualRequestFromStockCommand(request.Id, ManualRequestFixture.AdministratorId),
            CancellationToken.None);

        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be(Errors.ManualRequest.NothingInStock().Code);
        (await fixture.Context.ManualBookRequests.SingleAsync()).Status.Should().Be(ManualBookRequestStatus.Searching);
        (await fixture.Context.BookMovements.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task Pick_OnFoundRequest_ReturnsInvalidTransition()
    {
        await using var fixture = await ManualRequestFixture.CreateAsync();
        var requester = await fixture.AddRequesterAsync();
        var book = await fixture.AddBookAsync(Isbn);
        var session = await fixture.AddSessionAsync(ScanMode.AvailableNow);
        var request = await fixture.AddEditionRequestAsync(requester.Id, Isbn);
        request.MarkFoundByScan(
            book.Id, session.Id, BookMovementId.CreateUnique(), ManualRequestFixture.VolunteerId,
            ManualRequestFixture.DefaultNow);
        await fixture.Context.SaveChangesAsync();
        var handler = new PickManualRequestFromStockCommandHandler(fixture.Context, fixture.CreateClock());

        var result = await handler.Handle(
            new PickManualRequestFromStockCommand(request.Id, ManualRequestFixture.AdministratorId),
            CancellationToken.None);

        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be(Errors.ManualRequest.InvalidTransition().Code);
        (await fixture.Context.BookMovements.CountAsync()).Should().Be(0);
    }

    private static AddManualBookRequestCommand CreateCommand(
        Guid requesterId,
        ManualBookRequestScope scope,
        string? workId = null,
        string? isbn = null,
        bool pickFromStock = false) => new(
        requesterId,
        scope,
        workId,
        isbn,
        "Le Petit Prince",
        "Antoine de Saint-Exupéry",
        "Gallimard",
        1943,
        pickFromStock,
        ManualRequestFixture.AdministratorId);
}
