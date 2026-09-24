using FluentAssertions;
using System.Data.Common;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Vole_Papillon_Damour.Application.MemberSelection.Commands.AddSelectionItem;
using Vole_Papillon_Damour.Application.MemberSelection.Common;
using Vole_Papillon_Damour.Application.MemberSelection.Queries.GetMySelection;
using Vole_Papillon_Damour.Application.tests.MemberSelection;
using Vole_Papillon_Damour.Domain.BookAggregate.ValueObjects;
using Vole_Papillon_Damour.Domain.MemberSelectionAggregate;
using Vole_Papillon_Damour.Domain.MemberSelectionAggregate.ValueObjects;
using Vole_Papillon_Damour.Domain.NotFoundReportAggregate.ValueObjects;
using Vole_Papillon_Damour.Domain.UserAggregate.ValueObjects;

namespace Vole_Papillon_Damour.Application.tests.MemberSelection;

public sealed class GetMySelectionQueryHandlerTests
{
    private static readonly Guid ExternalId = Guid.Parse("7f0b0f0e-0000-0000-0000-00000000c0de");
    private static readonly DateTime Now = new(2026, 9, 23, 10, 0, 0, DateTimeKind.Utc);

    [Theory]
    [InlineData(2, false, false, SelectionAvailability.Available)]
    [InlineData(0, true, false, SelectionAvailability.Announced)]
    [InlineData(0, false, false, SelectionAvailability.OutOfStock)]
    [InlineData(3, false, true, SelectionAvailability.Unavailable)]
    public async Task Handle_ProjectsEditionAvailability(
        int quantity,
        bool announced,
        bool hidden,
        SelectionAvailability expected)
    {
        await using var fixture = await MemberSelectionFixture.CreateAsync(Now);
        await fixture.AddBookAsync("9782070612758", quantityAvailable: quantity, hidden: false);
        if (announced)
        {
            await fixture.AddAnnouncementAsync("9782070612758");
        }

        await fixture.CreateAddHandler().Handle(Add(isbn: "9782070612758"), default);
        if (hidden)
        {
            await fixture.HideBookAsync("9782070612758");
        }

        var result = await fixture.CreateGetMySelectionHandler().Handle(Query(), default);

        result.Value.Items.Should().ContainSingle()
            .Which.Availability.Should().Be(expected);
    }

    [Fact]
    public async Task Handle_WhenRareBookSold_ReturnsRareSold()
    {
        await using var fixture = await MemberSelectionFixture.CreateAsync(Now);
        var rareBook = await fixture.AddRareBookAsync();
        await fixture.CreateAddHandler().Handle(Add(rareBookId: rareBook.Id.Value), default);
        await fixture.MarkRareBookSoldAsync(rareBook.Id);

        var result = await fixture.CreateGetMySelectionHandler().Handle(Query(), default);

        result.Value.Items.Single().Availability.Should().Be(SelectionAvailability.RareSold);
    }

    [Fact]
    public async Task Handle_ReturnsOnlyOwnItems_NewestFirst()
    {
        await using var fixture = await MemberSelectionFixture.CreateAsync(Now);
        await fixture.AddBookAsync("9782070612758");
        await fixture.AddBookAsync("9782070584628");
        await fixture.AddSelectionItemForOtherMemberAsync("9782070612758");
        await fixture.CreateAddHandler().Handle(Add(isbn: "9782070612758"), default);
        fixture.Clock.Advance(TimeSpan.FromMinutes(1));
        await fixture.CreateAddHandler().Handle(Add(isbn: "9782070584628"), default);

        var result = await fixture.CreateGetMySelectionHandler().Handle(Query(), default);

        result.Value.Items.Select(item => item.Isbn13).Should().Equal("9782070584628", "9782070612758");
    }

    [Fact]
    public async Task Handle_WhenEmpty_ReturnsEmptyList()
    {
        await using var fixture = await MemberSelectionFixture.CreateAsync(Now);

        var result = await fixture.CreateGetMySelectionHandler().Handle(Query(), default);

        result.IsError.Should().BeFalse();
        result.Value.Items.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_IncludesTheNextBookFairSummary()
    {
        await using var fixture = await MemberSelectionFixture.CreateAsync(Now);
        var nextFair = await fixture.AddFairAsync(new DateTimeOffset(Now.AddDays(30), TimeSpan.Zero));

        var result = await fixture.CreateGetMySelectionHandler().Handle(Query(), default);

        result.Value.NextFair.Should().BeEquivalentTo(
            new NextFairSummary(nextFair.Id.Value, nextFair.DateStart));
    }

    [Fact]
    public async Task Handle_ReturnsOpenReportOnLine()
    {
        await using var fixture = await MemberSelectionFixture.CreateAsync(Now);
        await fixture.AddBookAsync("9782070612758");
        await fixture.CreateAddHandler().Handle(Add(isbn: "9782070612758"), default);
        var report = await fixture.AddNotFoundReportAsync(
            UserId.Create(ExternalId), "9782070612758", Now.AddDays(-2));

        var result = await fixture.CreateGetMySelectionHandler().Handle(Query(), default);

        result.Value.Items.Single().NotFoundReport.Should().BeEquivalentTo(
            new NotFoundReportSummary(
                report.Id,
                "Open",
                new DateTimeOffset(report.ReportedAt),
                null));
    }

    [Fact]
    public async Task Handle_HidesReportsClosedMoreThan30DaysAgo()
    {
        await using var fixture = await MemberSelectionFixture.CreateAsync(Now);
        await fixture.AddBookAsync("9782070612758");
        await fixture.CreateAddHandler().Handle(Add(isbn: "9782070612758"), default);
        var report = await fixture.AddNotFoundReportAsync(
            UserId.Create(ExternalId), "9782070612758", Now.AddDays(-31));
        report.MarkFound(UserId.Create(Guid.NewGuid()), "Retrouvé", Now.AddDays(-30).AddHours(-1));
        await fixture.Context.SaveChangesAsync();

        var result = await fixture.CreateGetMySelectionHandler().Handle(Query(), default);

        result.Value.Items.Single().NotFoundReport.Should().BeNull();
    }

    [Fact]
    public async Task Handle_NeverReturnsCancelledReports()
    {
        await using var fixture = await MemberSelectionFixture.CreateAsync(Now);
        await fixture.AddBookAsync("9782070612758");
        await fixture.CreateAddHandler().Handle(Add(isbn: "9782070612758"), default);
        var report = await fixture.AddNotFoundReportAsync(
            UserId.Create(ExternalId), "9782070612758", Now.AddDays(-1));
        report.Cancel(Now.AddHours(-1));
        await fixture.Context.SaveChangesAsync();

        var result = await fixture.CreateGetMySelectionHandler().Handle(Query(), default);

        result.Value.Items.Single().NotFoundReport.Should().BeNull();
    }

    [Fact]
    public async Task Handle_UsesSingleQueryForReports()
    {
        var queryCounter = new NotFoundReportQueryCounter();
        await using var fixture = await MemberSelectionFixture.CreateAsync(Now, queryCounter);
        await fixture.AddBookAsync("9782070612758");
        await fixture.AddBookAsync("9782070584628");
        await fixture.CreateAddHandler().Handle(Add(isbn: "9782070612758"), default);
        await fixture.CreateAddHandler().Handle(Add(isbn: "9782070584628"), default);
        await fixture.AddNotFoundReportAsync(
            UserId.Create(ExternalId), "9782070612758", Now.AddHours(-1));
        await fixture.AddNotFoundReportAsync(
            UserId.Create(ExternalId), "9782070584628", Now.AddHours(-2));

        var result = await fixture.CreateGetMySelectionHandler().Handle(Query(), default);

        result.Value.Items.Should().HaveCount(2);
        queryCounter.ReportReadCount.Should().Be(1);
    }

    [Fact]
    public async Task Handle_UsesCanonicalEditionForReportState()
    {
        await using var fixture = await MemberSelectionFixture.CreateAsync(Now);
        const string redirectedIsbn = "9782070612758";
        const string canonicalIsbn = "9782070584628";
        await fixture.AddBookAsync(canonicalIsbn);
        await fixture.AddBookAsync(redirectedIsbn, redirectTo: canonicalIsbn);
        var user = await fixture.CreateMemberIdentityService().EnsureAsync(
            ExternalId, "camille@example.test", "Camille", null, default);
        Isbn13.TryCreate(redirectedIsbn, out var source).Should().BeTrue();
        var oldSelectionItem = MemberSelectionItem.CreateForEdition(
            Guid.NewGuid(), user.Id, source, Now.AddHours(-1));
        fixture.Context.MemberSelectionItems.Add(oldSelectionItem);
        await fixture.Context.SaveChangesAsync();
        var report = await fixture.AddNotFoundReportAsync(user.Id, canonicalIsbn, Now.AddMinutes(-30));

        var result = await fixture.CreateGetMySelectionHandler().Handle(Query(), default);

        result.Value.Items.Single().NotFoundReport!.Id.Should().Be(report.Id);
    }

    private sealed class NotFoundReportQueryCounter : DbCommandInterceptor
    {
        private int _reportReadCount;

        public int ReportReadCount => Volatile.Read(ref _reportReadCount);

        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
            DbCommand command,
            CommandEventData eventData,
            InterceptionResult<DbDataReader> result,
            CancellationToken cancellationToken = default)
        {
            if (command.CommandText.Contains("SELECT", StringComparison.OrdinalIgnoreCase) &&
                command.CommandText.Contains("BookNotFoundReports", StringComparison.OrdinalIgnoreCase))
            {
                Interlocked.Increment(ref _reportReadCount);
            }

            return ValueTask.FromResult(result);
        }
    }

    private static AddSelectionItemCommand Add(string? isbn = null, Guid? rareBookId = null) =>
        new(ExternalId, "camille@example.test", "Camille", null, isbn, rareBookId);

    private static GetMySelectionQuery Query() =>
        new(ExternalId, "camille@example.test", "Camille", null);
}

