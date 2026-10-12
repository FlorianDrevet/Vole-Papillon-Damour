using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using Vole_Papillon_Damour.Application.Common.Interfaces.Persistence;
using Vole_Papillon_Damour.Application.ManualRequests.Commands.CreateRequester;
using Vole_Papillon_Damour.Application.ManualRequests.Commands.DeleteRequester;
using Vole_Papillon_Damour.Application.ManualRequests.Commands.UpdateRequester;
using Vole_Papillon_Damour.Domain.BookAggregate.ValueObjects;
using Vole_Papillon_Damour.Domain.BookMovementAggregate.ValueObjects;
using Vole_Papillon_Damour.Domain.Common.Errors;
using Vole_Papillon_Damour.Domain.ManualRequestAggregate;
using Vole_Papillon_Damour.Domain.ManualRequestAggregate.ValueObjects;
using Vole_Papillon_Damour.Domain.ScanSessionAggregate.ValueObjects;

namespace Vole_Papillon_Damour.Application.tests.ManualRequests.Commands;

public sealed class RequesterCommandHandlerTests
{
    [Fact]
    public async Task Create_WithoutConsent_ReturnsConsentRequiredWithoutSaving()
    {
        await using var fixture = await ManualRequestFixture.CreateAsync();
        var handler = new CreateManualRequesterCommandHandler(fixture.Context, fixture.CreateClock());

        var result = await handler.Handle(
            new CreateManualRequesterCommand("Martin", "Jeanne", "0612345678", false,
                ManualRequestFixture.AdministratorId),
            CancellationToken.None);

        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be(Errors.ManualRequest.ConsentRequired().Code);
        (await fixture.Context.ManualRequesters.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task Create_WithInvalidPhone_ReturnsInvalidPhone()
    {
        await using var fixture = await ManualRequestFixture.CreateAsync();
        var handler = new CreateManualRequesterCommandHandler(fixture.Context, fixture.CreateClock());

        var result = await handler.Handle(
            new CreateManualRequesterCommand("Martin", "Jeanne", "not a phone", true,
                ManualRequestFixture.AdministratorId),
            CancellationToken.None);

        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be(Errors.ManualRequest.InvalidPhone().Code);
        (await fixture.Context.ManualRequesters.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task Create_WithInvalidName_ReturnsInvalidName()
    {
        await using var fixture = await ManualRequestFixture.CreateAsync();
        var handler = new CreateManualRequesterCommandHandler(fixture.Context, fixture.CreateClock());

        var result = await handler.Handle(
            new CreateManualRequesterCommand(" ", "Jeanne", "0612345678", true,
                ManualRequestFixture.AdministratorId),
            CancellationToken.None);

        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be(Errors.ManualRequest.InvalidName().Code);
        (await fixture.Context.ManualRequesters.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task Create_Valid_PersistsNormalizedPhone()
    {
        await using var fixture = await ManualRequestFixture.CreateAsync();
        var handler = new CreateManualRequesterCommandHandler(fixture.Context, fixture.CreateClock());

        var result = await handler.Handle(
            new CreateManualRequesterCommand(" Martin ", " Jeanne ", "06 12-34.56 (78)", true,
                ManualRequestFixture.AdministratorId),
            CancellationToken.None);

        result.IsError.Should().BeFalse();
        var requester = await fixture.Context.ManualRequesters.SingleAsync();
        requester.Id.Should().Be(result.Value);
        requester.LastName.Should().Be("Martin");
        requester.FirstName.Should().Be("Jeanne");
        requester.Phone.Should().Be("0612345678");
        requester.ConsentRecordedAt.Should().Be(ManualRequestFixture.DefaultNow);
        requester.ConsentRecordedBy.Should().Be(ManualRequestFixture.AdministratorId);
    }

    [Fact]
    public async Task Update_ErasedRequester_ReturnsRequesterErased()
    {
        await using var fixture = await ManualRequestFixture.CreateAsync();
        var requester = await fixture.AddRequesterAsync();
        requester.Erase(ManualRequestFixture.DefaultNow.AddMinutes(1));
        await fixture.Context.SaveChangesAsync();
        var handler = new UpdateManualRequesterCommandHandler(fixture.Context, fixture.CreateClock());

        var result = await handler.Handle(
            new UpdateManualRequesterCommand(requester.Id, "Martin", "Jeanne", "0612345678",
                ManualRequestFixture.AdministratorId),
            CancellationToken.None);

        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be(Errors.ManualRequest.RequesterErased().Code);
        (await fixture.Context.ManualRequesters.SingleAsync()).IsErased.Should().BeTrue();
    }

    [Fact]
    public async Task Update_Valid_NormalizesPhoneAndPersistsContact()
    {
        await using var fixture = await ManualRequestFixture.CreateAsync();
        var requester = await fixture.AddRequesterAsync();
        var handler = new UpdateManualRequesterCommandHandler(fixture.Context, fixture.CreateClock());

        var result = await handler.Handle(
            new UpdateManualRequesterCommand(requester.Id, " Durand ", " Alice ", "+33 (0)6 12-34-56-78",
                ManualRequestFixture.AdministratorId),
            CancellationToken.None);

        result.IsError.Should().BeFalse();
        var savedRequester = await fixture.Context.ManualRequesters.SingleAsync();
        savedRequester.LastName.Should().Be("Durand");
        savedRequester.FirstName.Should().Be("Alice");
        savedRequester.Phone.Should().Be("+330612345678");
        savedRequester.UpdatedAt.Should().Be(ManualRequestFixture.DefaultNow);
    }

    [Fact]
    public async Task Delete_CancelsOpenRequestsReleasesFoundCopyAndErases()
    {
        await using var fixture = await ManualRequestFixture.CreateAsync();
        var requester = await fixture.AddRequesterAsync();
        var searching = await fixture.AddWorkRequestAsync(requester.Id);
        var found = await fixture.AddEditionRequestAsync(requester.Id, "9782070612758");
        var given = await fixture.AddEditionRequestAsync(requester.Id, "9782070363735");
        var book = await fixture.AddBookAsync("9782070612758");
        var session = await fixture.AddSessionAsync(ScanMode.AvailableNow);
        found.MarkFoundByScan(
            book.Id,
            session.Id,
            BookMovementId.CreateUnique(),
            ManualRequestFixture.VolunteerId,
            ManualRequestFixture.DefaultNow);
        given.MarkFoundByScan(
            book.Id,
            session.Id,
            BookMovementId.CreateUnique(),
            ManualRequestFixture.VolunteerId,
            ManualRequestFixture.DefaultNow);
        given.MarkGiven(ManualRequestFixture.AdministratorId, ManualRequestFixture.DefaultNow);
        await fixture.Context.SaveChangesAsync();
        var alertOutbox = Substitute.For<IBookAlertOutbox>();
        alertOutbox.QueueForReleaseAsync(
                Arg.Any<BookMovementId>(), Arg.Any<DateTime>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(1));
        var handler = new DeleteManualRequesterCommandHandler(
            fixture.Context, fixture.CreateClock(), alertOutbox);

        var result = await handler.Handle(
            new DeleteManualRequesterCommand(requester.Id, ManualRequestFixture.AdministratorId),
            CancellationToken.None);

        result.IsError.Should().BeFalse();
        result.Value.CancelledRequestCount.Should().Be(2);
        result.Value.NotifiedMemberCount.Should().Be(1);
        (await fixture.Context.Books.SingleAsync(candidate => candidate.Id == book.Id)).QuantityAvailable.Should().Be(1);
        var savedRequester = await fixture.Context.ManualRequesters.SingleAsync();
        savedRequester.IsErased.Should().BeTrue();
        savedRequester.LastName.Should().BeNull();
        savedRequester.FirstName.Should().BeNull();
        savedRequester.Phone.Should().BeNull();
        (await fixture.Context.ManualBookRequests.SingleAsync(candidate => candidate.Id == searching.Id)).Status
            .Should().Be(ManualBookRequestStatus.Cancelled);
        var savedFound = await fixture.Context.ManualBookRequests.SingleAsync(candidate => candidate.Id == found.Id);
        savedFound.Status.Should().Be(ManualBookRequestStatus.Cancelled);
        savedFound.ReleaseMovementId.Should().NotBeNull();
        (await fixture.Context.ManualBookRequests.SingleAsync(candidate => candidate.Id == given.Id)).Status
            .Should().Be(ManualBookRequestStatus.Given);
        var release = await fixture.Context.BookMovements.SingleAsync();
        release.Type.Should().Be(BookMovementType.DirectEntry);
        await alertOutbox.Received(1).QueueForReleaseAsync(
            savedFound.ReleaseMovementId!,
            ManualRequestFixture.DefaultNow,
            Arg.Any<CancellationToken>());
    }
}
