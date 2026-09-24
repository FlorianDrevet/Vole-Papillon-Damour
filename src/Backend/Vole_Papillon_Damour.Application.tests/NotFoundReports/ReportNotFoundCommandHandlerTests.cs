using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Vole_Papillon_Damour.Application.MemberSelection.Commands.AddSelectionItem;
using Vole_Papillon_Damour.Application.NotFoundReports.Commands.ReportNotFound;
using Vole_Papillon_Damour.Application.tests.MemberSelection;
using Vole_Papillon_Damour.Domain.AssociationSettingsAggregate;
using Vole_Papillon_Damour.Domain.Common.Errors;
using Vole_Papillon_Damour.Domain.MemberSelectionAggregate;
using Vole_Papillon_Damour.Domain.MemberSelectionAggregate.ValueObjects;
using Vole_Papillon_Damour.Domain.NotFoundReportAggregate.ValueObjects;
using Vole_Papillon_Damour.Domain.RareBookAggregate.ValueObjects;
using Vole_Papillon_Damour.Domain.UserAggregate.ValueObjects;

namespace Vole_Papillon_Damour.Application.tests.NotFoundReports;

public sealed class ReportNotFoundCommandHandlerTests
{
    private static readonly DateTime Now = new(2026, 9, 24, 12, 0, 0, DateTimeKind.Utc);
    private static readonly Guid ExternalId = Guid.Parse("1282a29b-9166-4f89-a253-d342fd780c5a");
    private const string Email = "member@example.test";
    private const string AvailableIsbn = "9782070612758";
    private const string OtherIsbn = "9782070584628";

    [Fact]
    public async Task Handle_OnAvailableSelectedEdition_CreatesOpenReport()
    {
        await using var fixture = await MemberSelectionFixture.CreateAsync(Now);
        await fixture.AddBookAsync(AvailableIsbn, quantityAvailable: 3);
        var selectionItemId = await AddSelectionItemAsync(fixture, AvailableIsbn);

        var result = await fixture.CreateReportNotFoundHandler().Handle(
            Command(selectionItemId, NotFoundReportLocation.Premises, "  Introuvable au rayon  "),
            default);

        result.IsError.Should().BeFalse();
        result.Value.AlreadyOpen.Should().BeFalse();
        var report = await fixture.Context.BookNotFoundReports.SingleAsync();
        report.Id.Should().Be(result.Value.ReportId);
        report.UserId.Should().Be(UserId.Create(ExternalId));
        report.Isbn13!.Value.Value.Should().Be(AvailableIsbn);
        report.Location.Should().Be(NotFoundReportLocation.Premises);
        report.Comment.Should().Be("Introuvable au rayon");
        report.Status.Should().Be(NotFoundReportStatus.Open);
        report.ReportedAt.Should().Be(Now);
        result.Value.ReportedAt.Should().Be(new DateTimeOffset(Now));
    }

    [Fact]
    public async Task Handle_WhenOpenReportExists_ReturnsExistingWithoutCreating()
    {
        await using var fixture = await MemberSelectionFixture.CreateAsync(Now);
        await fixture.AddBookAsync(AvailableIsbn);
        var selectionItemId = await AddSelectionItemAsync(fixture, AvailableIsbn);
        var existing = await fixture.AddNotFoundReportAsync(UserId.Create(ExternalId), AvailableIsbn, Now.AddHours(-1));

        var result = await fixture.CreateReportNotFoundHandler().Handle(Command(selectionItemId), default);

        result.IsError.Should().BeFalse();
        result.Value.ReportId.Should().Be(existing.Id);
        result.Value.AlreadyOpen.Should().BeTrue();
        (await fixture.Context.BookNotFoundReports.CountAsync()).Should().Be(1);
    }

    [Fact]
    public async Task Handle_OnPurchasedLine_ReturnsNotReportable()
    {
        await using var fixture = await MemberSelectionFixture.CreateAsync(Now);
        await fixture.AddBookAsync(AvailableIsbn);
        var selectionItemId = await AddSelectionItemAsync(fixture, AvailableIsbn);
        var selectionItem = await fixture.Context.MemberSelectionItems.SingleAsync(item => item.Id == selectionItemId);
        selectionItem.MarkPurchased(Now);
        await fixture.Context.SaveChangesAsync();

        var result = await fixture.CreateReportNotFoundHandler().Handle(Command(selectionItemId), default);

        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("NotFoundReport.NotReportable");
        (await fixture.Context.BookNotFoundReports.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task Handle_OnOutOfStockEdition_ReturnsNotReportable()
    {
        await using var fixture = await MemberSelectionFixture.CreateAsync(Now);
        await fixture.AddBookAsync(AvailableIsbn, quantityAvailable: 0);
        var selectionItemId = await AddSelectionItemAsync(fixture, AvailableIsbn);

        var result = await fixture.CreateReportNotFoundHandler().Handle(Command(selectionItemId), default);

        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("NotFoundReport.NotReportable");
    }

    [Fact]
    public async Task Handle_OnAnnouncedEdition_ReturnsNotReportable()
    {
        await using var fixture = await MemberSelectionFixture.CreateAsync(Now);
        await fixture.AddAnnouncementAsync(AvailableIsbn);
        var selectionItemId = await AddSelectionItemAsync(fixture, AvailableIsbn);

        var result = await fixture.CreateReportNotFoundHandler().Handle(Command(selectionItemId), default);

        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("NotFoundReport.NotReportable");
    }

    [Fact]
    public async Task Handle_OnSoldRareBook_ReturnsNotReportable()
    {
        await using var fixture = await MemberSelectionFixture.CreateAsync(Now);
        var rareBook = await fixture.AddRareBookAsync(sold: true);
        var selectionItemId = await AddSelectionItemAsync(fixture, rareBook.Id.Value);

        var result = await fixture.CreateReportNotFoundHandler().Handle(Command(selectionItemId), default);

        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("NotFoundReport.NotReportable");
    }

    [Fact]
    public async Task Handle_OnAvailableRareBook_CreatesOpenReport()
    {
        await using var fixture = await MemberSelectionFixture.CreateAsync(Now);
        var rareBook = await fixture.AddRareBookAsync();
        var selectionItemId = await AddSelectionItemAsync(fixture, rareBook.Id.Value);

        var result = await fixture.CreateReportNotFoundHandler().Handle(
            Command(selectionItemId, NotFoundReportLocation.Fair, null),
            default);

        result.IsError.Should().BeFalse();
        result.Value.AlreadyOpen.Should().BeFalse();
        var report = await fixture.Context.BookNotFoundReports.SingleAsync();
        report.RareBookId.Should().Be(rareBook.Id);
        report.Isbn13.Should().BeNull();
        report.Location.Should().Be(NotFoundReportLocation.Fair);
    }

    [Fact]
    public async Task Handle_WhenDailyLimitReached_ReturnsDailyLimitReached()
    {
        await using var fixture = await MemberSelectionFixture.CreateAsync(Now);
        await fixture.AddBookAsync(AvailableIsbn);
        var selectionItemId = await AddSelectionItemAsync(fixture, AvailableIsbn);
        await fixture.SetNotFoundReportDailyLimitAsync(UserId.Create(ExternalId), 1);
        var previous = await fixture.AddNotFoundReportAsync(UserId.Create(ExternalId), OtherIsbn, Now.AddHours(-1));
        previous.Cancel(Now.AddMinutes(-30));
        await fixture.Context.SaveChangesAsync();

        var result = await fixture.CreateReportNotFoundHandler().Handle(Command(selectionItemId), default);

        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("NotFoundReport.DailyLimitReached");
        (await fixture.Context.BookNotFoundReports.CountAsync()).Should().Be(1);
    }

    [Fact]
    public async Task Handle_ReportsOlderThan24h_DoNotCountTowardsLimit()
    {
        await using var fixture = await MemberSelectionFixture.CreateAsync(Now);
        await fixture.AddBookAsync(AvailableIsbn);
        var selectionItemId = await AddSelectionItemAsync(fixture, AvailableIsbn);
        await fixture.SetNotFoundReportDailyLimitAsync(UserId.Create(ExternalId), 1);
        await fixture.AddNotFoundReportAsync(
            UserId.Create(ExternalId),
            OtherIsbn,
            Now.AddHours(-24).AddTicks(-1));

        var result = await fixture.CreateReportNotFoundHandler().Handle(Command(selectionItemId), default);

        result.IsError.Should().BeFalse();
        result.Value.AlreadyOpen.Should().BeFalse();
        (await fixture.Context.BookNotFoundReports.CountAsync()).Should().Be(2);
    }

    [Fact]
    public async Task Handle_OnAnotherMembersSelectionItem_ReturnsNotFound()
    {
        await using var fixture = await MemberSelectionFixture.CreateAsync(Now);
        await fixture.AddBookAsync(AvailableIsbn);
        var foreignItem = await fixture.AddSelectionItemForOtherMemberAsync(AvailableIsbn);

        var result = await fixture.CreateReportNotFoundHandler().Handle(Command(foreignItem.Id), default);

        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("MemberSelection.NotFound");
    }

    [Fact]
    public async Task Handle_DoesNotChangeBookQuantity()
    {
        await using var fixture = await MemberSelectionFixture.CreateAsync(Now);
        var book = await fixture.AddBookAsync(AvailableIsbn, quantityAvailable: 4);
        var selectionItemId = await AddSelectionItemAsync(fixture, AvailableIsbn);

        var result = await fixture.CreateReportNotFoundHandler().Handle(Command(selectionItemId), default);

        result.IsError.Should().BeFalse();
        (await fixture.Context.Books.SingleAsync(candidate => candidate.Id == book.Id))
            .QuantityAvailable.Should().Be(4);
    }

    [Fact]
    public void Validator_RejectsCommentLongerThan280Characters()
    {
        var result = new ReportNotFoundCommandValidator().Validate(
            Command(Guid.NewGuid(), comment: new string('x', 281)));

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(error => error.PropertyName == "Comment");
    }

    [Fact]
    public void Validator_RejectsUndefinedLocation()
    {
        var result = new ReportNotFoundCommandValidator().Validate(
            Command(Guid.NewGuid(), (NotFoundReportLocation)byte.MaxValue));

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(error => error.PropertyName == "Location");
    }

    private static async Task<Guid> AddSelectionItemAsync(MemberSelectionFixture fixture, string isbn)
    {
        var result = await fixture.CreateAddHandler().Handle(
            new AddSelectionItemCommand(ExternalId, Email, "Test", "Member", isbn, null),
            default);
        result.IsError.Should().BeFalse();
        return result.Value.Id;
    }

    private static async Task<Guid> AddSelectionItemAsync(MemberSelectionFixture fixture, Guid rareBookId)
    {
        var result = await fixture.CreateAddHandler().Handle(
            new AddSelectionItemCommand(ExternalId, Email, "Test", "Member", null, rareBookId),
            default);
        result.IsError.Should().BeFalse();
        return result.Value.Id;
    }

    private static ReportNotFoundCommand Command(
        Guid selectionItemId,
        NotFoundReportLocation? location = null,
        string? comment = null) =>
        new(ExternalId, Email, "Test", "Member", selectionItemId, location, comment);
}
