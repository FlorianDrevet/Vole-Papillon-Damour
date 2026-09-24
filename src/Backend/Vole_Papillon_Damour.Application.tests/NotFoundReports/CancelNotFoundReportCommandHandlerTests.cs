using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Vole_Papillon_Damour.Application.tests.MemberSelection;
using Vole_Papillon_Damour.Domain.NotFoundReportAggregate.ValueObjects;
using Vole_Papillon_Damour.Domain.UserAggregate.ValueObjects;

namespace Vole_Papillon_Damour.Application.tests.NotFoundReports;

public sealed class CancelNotFoundReportCommandHandlerTests
{
    private static readonly DateTime Now = new(2026, 9, 24, 12, 0, 0, DateTimeKind.Utc);
    private static readonly Guid ExternalId = Guid.Parse("1282a29b-9166-4f89-a253-d342fd780c5a");
    private const string Email = "member@example.test";
    private const string Isbn = "9782070612758";

    [Fact]
    public async Task Cancel_OpenReport_SetsCancelled()
    {
        await using var fixture = await MemberSelectionFixture.CreateAsync(Now);
        var user = await fixture.CreateMemberIdentityService().EnsureAsync(
            ExternalId, Email, "Test", "Member", default);
        var report = await fixture.AddNotFoundReportAsync(user.Id, Isbn, Now.AddHours(-1));

        var result = await fixture.CreateCancelNotFoundReportHandler().Handle(Command(report.Id), default);

        result.IsError.Should().BeFalse();
        (await fixture.Context.BookNotFoundReports.SingleAsync()).Status
            .Should().Be(NotFoundReportStatus.Cancelled);
        report.ClosedAt.Should().Be(Now);
    }

    [Fact]
    public async Task Cancel_ClosedReport_ReturnsConflict()
    {
        await using var fixture = await MemberSelectionFixture.CreateAsync(Now);
        var user = await fixture.CreateMemberIdentityService().EnsureAsync(
            ExternalId, Email, "Test", "Member", default);
        var report = await fixture.AddNotFoundReportAsync(user.Id, Isbn, Now.AddHours(-1));
        report.MarkFound(UserId.Create(Guid.NewGuid()), "Contrôlé", Now.AddMinutes(-1));
        await fixture.Context.SaveChangesAsync();

        var result = await fixture.CreateCancelNotFoundReportHandler().Handle(Command(report.Id), default);

        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("NotFoundReport.AlreadyClosed");
        report.Status.Should().Be(NotFoundReportStatus.Found);
    }

    [Fact]
    public async Task Cancel_OtherMembersReport_ReturnsNotFound()
    {
        await using var fixture = await MemberSelectionFixture.CreateAsync(Now);
        var otherUserId = UserId.Create(Guid.NewGuid());
        var report = await fixture.AddNotFoundReportAsync(otherUserId, Isbn, Now.AddHours(-1));

        var result = await fixture.CreateCancelNotFoundReportHandler().Handle(Command(report.Id), default);

        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("NotFoundReport.NotFound");
        report.Status.Should().Be(NotFoundReportStatus.Open);
    }

    private static Vole_Papillon_Damour.Application.NotFoundReports.Commands.CancelNotFoundReport.CancelNotFoundReportCommand
        Command(Guid reportId) => new(ExternalId, Email, "Test", "Member", reportId);
}
