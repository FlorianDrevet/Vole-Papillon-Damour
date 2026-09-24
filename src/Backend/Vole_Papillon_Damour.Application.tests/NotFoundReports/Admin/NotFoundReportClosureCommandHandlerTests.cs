using ErrorOr;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Vole_Papillon_Damour.Application.Books.Commands.Admin;
using Vole_Papillon_Damour.Application.NotFoundReports.Commands.Admin.DismissNotFound;
using Vole_Papillon_Damour.Application.NotFoundReports.Commands.Admin.MarkFound;
using Vole_Papillon_Damour.Application.NotFoundReports.Commands.Admin.WithdrawNotFound;
using Vole_Papillon_Damour.Application.NotFoundReports.Common;
using Vole_Papillon_Damour.Application.tests.MemberSelection;
using Vole_Papillon_Damour.Domain.BookMovementAggregate.ValueObjects;
using Vole_Papillon_Damour.Domain.NotFoundReportAggregate;
using Vole_Papillon_Damour.Domain.NotFoundReportAggregate.ValueObjects;
using Vole_Papillon_Damour.Domain.RareBookAggregate.ValueObjects;
using Vole_Papillon_Damour.Domain.UserAggregate.ValueObjects;

namespace Vole_Papillon_Damour.Application.tests.NotFoundReports.Admin;

public sealed class NotFoundReportClosureCommandHandlerTests
{
    private static readonly DateTime Now = new(2026, 9, 24, 14, 0, 0, DateTimeKind.Utc);
    private static readonly UserId Author = UserId.Create(Guid.Parse("ce29f0dc-e10e-408c-a94f-41ec9b83fe57"));
    private const string Isbn = "9782070612758";
    private const string OtherIsbn = "9782070584628";
    private const string Note = "Vérifié au rayon : exemplaires absents";

    [Fact]
    public async Task MarkFound_ClosesAllOpenReportsAndKeepsStock()
    {
        await using var fixture = await MemberSelectionFixture.CreateAsync(Now);
        var book = await fixture.AddBookAsync(Isbn, quantityAvailable: 3);
        await fixture.AddNotFoundReportAsync(UserId.CreateUnique(), Isbn, Now.AddHours(-3));
        await fixture.AddNotFoundReportAsync(UserId.CreateUnique(), Isbn, Now.AddHours(-2));

        var result = await MarkFoundHandler(fixture).Handle(
            new MarkNotFoundTargetFoundCommand(Edition(Isbn), "Retrouvé", Author), default);

        result.IsError.Should().BeFalse();
        result.Value.ClosedReportCount.Should().Be(2);
        result.Value.WithdrawnQuantity.Should().BeNull();
        result.Value.QuantityAvailable.Should().Be(3);
        result.Value.MovementId.Should().BeNull();
        (await fixture.Context.Books.SingleAsync(candidate => candidate.Id == book.Id))
            .QuantityAvailable.Should().Be(3);
        (await fixture.Context.BookNotFoundReports.ToListAsync())
            .Should().OnlyContain(report => report.Status == NotFoundReportStatus.Found);
        (await fixture.Context.BookMovements.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task Withdraw_WithZeroFound_WithdrawsAllAvailable()
    {
        await using var fixture = await MemberSelectionFixture.CreateAsync(Now);
        var book = await fixture.AddBookAsync(Isbn, quantityAvailable: 2);
        await fixture.AddNotFoundReportAsync(UserId.CreateUnique(), Isbn, Now.AddHours(-1));
        await fixture.AddNotFoundReportAsync(UserId.CreateUnique(), Isbn, Now.AddMinutes(-30));

        var result = await WithdrawHandler(fixture).Handle(
            WithdrawCommand(Edition(Isbn), quantityFound: 0), default);

        result.IsError.Should().BeFalse();
        result.Value.ClosedReportCount.Should().Be(2);
        result.Value.WithdrawnQuantity.Should().Be(2);
        result.Value.QuantityAvailable.Should().Be(0);
        result.Value.MovementId.Should().NotBeNull();
        (await fixture.Context.Books.SingleAsync(candidate => candidate.Id == book.Id))
            .QuantityAvailable.Should().Be(0);
        (await fixture.Context.BookNotFoundReports.ToListAsync())
            .Should().OnlyContain(report => report.Status == NotFoundReportStatus.Withdrawn);
    }

    [Fact]
    public async Task Withdraw_WithPartialFound_WithdrawsDifference()
    {
        await using var fixture = await MemberSelectionFixture.CreateAsync(Now);
        var book = await fixture.AddBookAsync(Isbn, quantityAvailable: 4);
        await fixture.AddNotFoundReportAsync(UserId.CreateUnique(), Isbn, Now.AddHours(-1));

        var result = await WithdrawHandler(fixture).Handle(
            WithdrawCommand(Edition(Isbn), quantityFound: 1), default);

        result.IsError.Should().BeFalse();
        result.Value.WithdrawnQuantity.Should().Be(3);
        result.Value.QuantityAvailable.Should().Be(1);
        (await fixture.Context.Books.SingleAsync(candidate => candidate.Id == book.Id))
            .QuantityAvailable.Should().Be(1);
    }

    [Fact]
    public async Task Withdraw_WithAllFound_ClosesAsFoundWithoutMovement()
    {
        await using var fixture = await MemberSelectionFixture.CreateAsync(Now);
        var book = await fixture.AddBookAsync(Isbn, quantityAvailable: 2);
        await fixture.AddNotFoundReportAsync(UserId.CreateUnique(), Isbn, Now.AddHours(-1));

        var result = await WithdrawHandler(fixture).Handle(
            WithdrawCommand(Edition(Isbn), quantityFound: 2), default);

        result.IsError.Should().BeFalse();
        result.Value.WithdrawnQuantity.Should().Be(0);
        result.Value.QuantityAvailable.Should().Be(2);
        result.Value.MovementId.Should().BeNull();
        (await fixture.Context.BookNotFoundReports.SingleAsync()).Status.Should().Be(NotFoundReportStatus.Found);
        (await fixture.Context.BookMovements.CountAsync()).Should().Be(0);
        (await fixture.Context.Books.SingleAsync(candidate => candidate.Id == book.Id))
            .QuantityAvailable.Should().Be(2);
    }

    [Fact]
    public async Task Withdraw_WithFoundAboveAvailable_Returns400()
    {
        await using var fixture = await MemberSelectionFixture.CreateAsync(Now);
        await fixture.AddBookAsync(Isbn, quantityAvailable: 2);
        await fixture.AddNotFoundReportAsync(UserId.CreateUnique(), Isbn, Now.AddHours(-1));

        var result = await WithdrawHandler(fixture).Handle(
            WithdrawCommand(Edition(Isbn), quantityFound: 3), default);

        result.IsError.Should().BeTrue();
        result.FirstError.Type.Should().Be(ErrorType.Validation);
        (await fixture.Context.BookNotFoundReports.SingleAsync()).Status.Should().Be(NotFoundReportStatus.Open);
        (await fixture.Context.BookMovements.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task Withdraw_CreatesWithdrawalMovementWithNoteAndAuthor()
    {
        await using var fixture = await MemberSelectionFixture.CreateAsync(Now);
        await fixture.AddBookAsync(Isbn, quantityAvailable: 1);
        await fixture.AddNotFoundReportAsync(UserId.CreateUnique(), Isbn, Now.AddHours(-1));

        var result = await WithdrawHandler(fixture).Handle(
            WithdrawCommand(Edition(Isbn), quantityFound: 0), default);

        result.IsError.Should().BeFalse();
        var movement = await fixture.Context.BookMovements.SingleAsync();
        movement.Type.Should().Be(BookMovementType.Withdrawal);
        movement.Quantity.Should().Be(-1);
        movement.Note.Should().Be(Note);
        movement.VolunteerId.Should().Be(Author);
        movement.Isbn13.Value.Should().Be(Isbn);
        movement.OccurredAt.Should().Be(Now);
        result.Value.MovementId.Should().Be(movement.Id.Value);
    }

    [Fact]
    public async Task Withdraw_OnRedirectedIsbn_TargetsCanonicalBook()
    {
        await using var fixture = await MemberSelectionFixture.CreateAsync(Now);
        var canonical = await fixture.AddBookAsync(OtherIsbn, quantityAvailable: 3);
        var redirected = await fixture.AddBookAsync(Isbn, quantityAvailable: 1, redirectTo: OtherIsbn);
        await fixture.AddNotFoundReportAsync(UserId.CreateUnique(), OtherIsbn, Now.AddHours(-1));

        var result = await WithdrawHandler(fixture).Handle(
            WithdrawCommand(Edition(Isbn), quantityFound: 0), default);

        result.IsError.Should().BeFalse();
        (await fixture.Context.BookMovements.SingleAsync()).Isbn13.Value.Should().Be(OtherIsbn);
        (await fixture.Context.Books.SingleAsync(book => book.Id == canonical.Id))
            .QuantityAvailable.Should().Be(0);
        (await fixture.Context.Books.SingleAsync(book => book.Id == redirected.Id))
            .QuantityAvailable.Should().Be(1);
    }

    [Fact]
    public async Task Withdraw_OnRareBook_UnpublishesAndClosesReports()
    {
        await using var fixture = await MemberSelectionFixture.CreateAsync(Now);
        var rareBook = await fixture.AddRareBookAsync();
        var report = BookNotFoundReport.CreateForRareBook(
            Guid.NewGuid(), UserId.CreateUnique(), rareBook.Id, null, null, Now.AddHours(-1));
        fixture.Context.BookNotFoundReports.Add(report);
        await fixture.Context.SaveChangesAsync();

        var result = await WithdrawHandler(fixture).Handle(
            WithdrawCommand(Rare(rareBook.Id.Value), quantityFound: 999), default);

        result.IsError.Should().BeFalse();
        result.Value.ClosedReportCount.Should().Be(1);
        result.Value.WithdrawnQuantity.Should().BeNull();
        result.Value.QuantityAvailable.Should().BeNull();
        result.Value.MovementId.Should().BeNull();
        (await fixture.Context.RareBooks.SingleAsync(book => book.Id == rareBook.Id))
            .Status.Should().Be(RareBookStatus.Draft);
        report.Status.Should().Be(NotFoundReportStatus.Withdrawn);
        report.WithdrawalReason.Should().Be(NotFoundWithdrawalReason.NotFoundOnShelf);
        (await fixture.Context.BookMovements.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task Dismiss_WithoutNote_Returns400()
    {
        await using var fixture = await MemberSelectionFixture.CreateAsync(Now);
        await fixture.AddBookAsync(Isbn, quantityAvailable: 1);
        await fixture.AddNotFoundReportAsync(UserId.CreateUnique(), Isbn, Now.AddHours(-1));

        var result = await DismissHandler(fixture).Handle(
            new DismissNotFoundTargetCommand(Edition(Isbn), "  ", Author), default);

        result.IsError.Should().BeTrue();
        result.FirstError.Type.Should().Be(ErrorType.Validation);
        (await fixture.Context.BookNotFoundReports.SingleAsync()).Status.Should().Be(NotFoundReportStatus.Open);
    }

    [Fact]
    public async Task AnyClosure_WithoutOpenReports_ReturnsConflict()
    {
        await using var fixture = await MemberSelectionFixture.CreateAsync(Now);
        await fixture.AddBookAsync(Isbn, quantityAvailable: 1);

        var result = await MarkFoundHandler(fixture).Handle(
            new MarkNotFoundTargetFoundCommand(Edition(Isbn), null, Author), default);

        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("NotFoundReport.NothingToClose");
        result.FirstError.Type.Should().Be(ErrorType.Conflict);
    }

    [Fact]
    public async Task AnyClosure_LeavesOtherTargetsReportsOpen()
    {
        await using var fixture = await MemberSelectionFixture.CreateAsync(Now);
        await fixture.AddBookAsync(Isbn, quantityAvailable: 2);
        await fixture.AddBookAsync(OtherIsbn, quantityAvailable: 2);
        await fixture.AddNotFoundReportAsync(UserId.CreateUnique(), Isbn, Now.AddHours(-1));
        var otherReport = await fixture.AddNotFoundReportAsync(UserId.CreateUnique(), OtherIsbn, Now.AddHours(-1));

        var result = await MarkFoundHandler(fixture).Handle(
            new MarkNotFoundTargetFoundCommand(Edition(Isbn), null, Author), default);

        result.IsError.Should().BeFalse();
        (await fixture.Context.BookNotFoundReports.SingleAsync(report => report.Id == otherReport.Id))
            .Status.Should().Be(NotFoundReportStatus.Open);
    }

    [Fact]
    public async Task Dismiss_WithNote_ClosesReportsWithoutChangingStock()
    {
        await using var fixture = await MemberSelectionFixture.CreateAsync(Now);
        var book = await fixture.AddBookAsync(Isbn, quantityAvailable: 2);
        await fixture.AddNotFoundReportAsync(UserId.CreateUnique(), Isbn, Now.AddHours(-1));

        var result = await DismissHandler(fixture).Handle(
            new DismissNotFoundTargetCommand(Edition(Isbn), "Signalement invérifiable", Author), default);

        result.IsError.Should().BeFalse();
        (await fixture.Context.BookNotFoundReports.SingleAsync()).Status.Should().Be(NotFoundReportStatus.Dismissed);
        (await fixture.Context.Books.SingleAsync(candidate => candidate.Id == book.Id))
            .QuantityAvailable.Should().Be(2);
        (await fixture.Context.BookMovements.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task WithdrawBookCommandHandler_StillReturnsAdminBookOperationResult()
    {
        await using var fixture = await MemberSelectionFixture.CreateAsync(Now);
        await fixture.AddBookAsync(Isbn, quantityAvailable: 2);

        var result = await new WithdrawBookCommandHandler(
            fixture.Context, fixture.Clock, new NotFoundReportLapser(fixture.Context)).Handle(
            new WithdrawBookCommand(Isbn, 1, Note, Author), default);

        result.IsError.Should().BeFalse();
        result.Value.Isbn13.Should().Be(Isbn);
        result.Value.QuantityAvailable.Should().Be(1);
        result.Value.MovementId.Should().NotBeNull();
        (await fixture.Context.BookMovements.SingleAsync()).Quantity.Should().Be(-1);
    }

    [Fact]
    public async Task WithdrawBook_ToZero_LapsesOpenReports()
    {
        await using var fixture = await MemberSelectionFixture.CreateAsync(Now);
        await fixture.AddBookAsync(Isbn, quantityAvailable: 1);
        var report = await fixture.AddNotFoundReportAsync(UserId.CreateUnique(), Isbn, Now.AddHours(-1));

        var result = await new WithdrawBookCommandHandler(
            fixture.Context, fixture.Clock, new NotFoundReportLapser(fixture.Context)).Handle(
            new WithdrawBookCommand(Isbn, 1, Note, Author), default);

        result.IsError.Should().BeFalse();
        (await fixture.Context.BookNotFoundReports.SingleAsync(candidate => candidate.Id == report.Id))
            .Status.Should().Be(NotFoundReportStatus.Lapsed);
    }

    [Fact]
    public async Task MergeBooks_MovesOpenReportsToCanonicalIsbn()
    {
        await using var fixture = await MemberSelectionFixture.CreateAsync(Now);
        await fixture.AddBookAsync(Isbn, quantityAvailable: 0);
        await fixture.AddBookAsync(OtherIsbn, quantityAvailable: 0);
        var report = await fixture.AddNotFoundReportAsync(UserId.CreateUnique(), Isbn, Now.AddHours(-1));

        var result = await new MergeBooksCommandHandler(
            fixture.Context, fixture.Clock, new NotFoundReportLapser(fixture.Context)).Handle(
            new MergeBooksCommand(Isbn, OtherIsbn, Note, Author), default);

        result.IsError.Should().BeFalse();
        var persisted = await fixture.Context.BookNotFoundReports.SingleAsync(candidate => candidate.Id == report.Id);
        persisted.Isbn13.Should().NotBeNull();
        persisted.Isbn13!.Value.Value.Should().Be(OtherIsbn);
        persisted.Status.Should().Be(NotFoundReportStatus.Lapsed);
    }

    private static MarkNotFoundTargetFoundCommandHandler MarkFoundHandler(MemberSelectionFixture fixture) =>
        new(fixture.Context, fixture.Clock,
            Microsoft.Extensions.Logging.Abstractions.NullLogger<MarkNotFoundTargetFoundCommandHandler>.Instance);

    private static WithdrawNotFoundTargetCommandHandler WithdrawHandler(MemberSelectionFixture fixture) =>
        new(fixture.Context, fixture.Clock,
            Microsoft.Extensions.Logging.Abstractions.NullLogger<WithdrawNotFoundTargetCommandHandler>.Instance);

    private static DismissNotFoundTargetCommandHandler DismissHandler(MemberSelectionFixture fixture) =>
        new(fixture.Context, fixture.Clock,
            Microsoft.Extensions.Logging.Abstractions.NullLogger<DismissNotFoundTargetCommandHandler>.Instance);

    private static WithdrawNotFoundTargetCommand WithdrawCommand(
        NotFoundReportTargetRef target,
        int quantityFound) =>
        new(target, quantityFound, NotFoundWithdrawalReason.NotFoundOnShelf, Note, Author);

    private static NotFoundReportTargetRef Edition(string isbn) => new("edition", isbn);

    private static NotFoundReportTargetRef Rare(Guid id) => new("rare", id.ToString("D"));
}
