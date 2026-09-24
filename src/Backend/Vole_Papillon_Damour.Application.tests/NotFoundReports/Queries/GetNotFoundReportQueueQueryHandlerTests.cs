using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Vole_Papillon_Damour.Application.NotFoundReports.Queries.GetNotFoundReportQueue;
using Vole_Papillon_Damour.Application.NotFoundReports.Queries.GetNotFoundReportSummary;
using Vole_Papillon_Damour.Application.NotFoundReports.Queries.GetClosedNotFoundReports;
using Vole_Papillon_Damour.Application.tests.MemberSelection;
using Vole_Papillon_Damour.Domain.NotFoundReportAggregate;
using Vole_Papillon_Damour.Domain.NotFoundReportAggregate.ValueObjects;
using Vole_Papillon_Damour.Domain.UserAggregate;
using Vole_Papillon_Damour.Domain.UserAggregate.ValueObjects;

namespace Vole_Papillon_Damour.Application.tests.NotFoundReports.Queries;

public sealed class GetNotFoundReportQueueQueryHandlerTests
{
    private static readonly DateTime Now = new(2026, 9, 24, 14, 0, 0, DateTimeKind.Utc);
    private const string Isbn = "9782070612758";
    private const string OtherIsbn = "9782070584628";

    [Fact]
    public async Task Queue_GroupsReportsByTarget()
    {
        await using var fixture = await MemberSelectionFixture.CreateAsync(Now);
        await fixture.AddBookAsync(Isbn, quantityAvailable: 2);
        var first = await AddEditionReportAsync(fixture, Isbn, UserId.CreateUnique(), Now.AddDays(-2), "Rayon B");
        var anonymized = await AddEditionReportAsync(fixture, Isbn, UserId.CreateUnique(), Now.AddDays(-1), "Local");
        anonymized.DetachMember();
        await fixture.Context.SaveChangesAsync();

        var result = await QueueHandler(fixture).Handle(
            new GetNotFoundReportQueueQuery("all", "most-reported", 1, 20, false), default);

        result.IsError.Should().BeFalse();
        result.Value.Items.Should().ContainSingle();
        var target = result.Value.Items[0];
        target.Isbn13.Should().Be(Isbn);
        target.ReportCount.Should().Be(2);
        target.MemberCount.Should().Be(2);
        target.Comments.Select(comment => comment.Text).Should().ContainSingle().Which.Should().Be("Rayon B");
    }

    [Fact]
    public async Task Queue_SortsByMostReportedByDefault()
    {
        await using var fixture = await MemberSelectionFixture.CreateAsync(Now);
        await fixture.AddBookAsync(Isbn);
        await fixture.AddBookAsync(OtherIsbn);
        await AddEditionReportAsync(fixture, Isbn, UserId.CreateUnique(), Now.AddHours(-2));
        await AddEditionReportAsync(fixture, Isbn, UserId.CreateUnique(), Now.AddHours(-1));
        await AddEditionReportAsync(fixture, OtherIsbn, UserId.CreateUnique(), Now.AddMinutes(-30));

        var result = await QueueHandler(fixture).Handle(
            new GetNotFoundReportQueueQuery("all", "", 1, 20, false), default);

        result.IsError.Should().BeFalse();
        result.Value.Items.Select(item => item.Isbn13).Should().ContainInOrder(Isbn, OtherIsbn);
    }

    [Fact]
    public async Task Queue_FlagsTargetsOlderThan7DaysAsOverdue()
    {
        await using var fixture = await MemberSelectionFixture.CreateAsync(Now);
        await fixture.AddBookAsync(Isbn);
        await fixture.AddBookAsync(OtherIsbn);
        await AddEditionReportAsync(fixture, Isbn, UserId.CreateUnique(), Now.AddDays(-8));
        await AddEditionReportAsync(fixture, OtherIsbn, UserId.CreateUnique(), Now.AddDays(-2));

        var result = await QueueHandler(fixture).Handle(
            new GetNotFoundReportQueueQuery("all", "oldest", 1, 20, false), default);

        result.IsError.Should().BeFalse();
        result.Value.OverdueTargetCount.Should().Be(1);
        result.Value.Items.Single(item => item.Isbn13 == Isbn).Overdue.Should().BeTrue();
        result.Value.Items.Single(item => item.Isbn13 == OtherIsbn).Overdue.Should().BeFalse();
    }

    [Fact]
    public async Task Queue_ExcludesClosedAndCancelled()
    {
        await using var fixture = await MemberSelectionFixture.CreateAsync(Now);
        await fixture.AddBookAsync(Isbn);
        await AddEditionReportAsync(fixture, Isbn, UserId.CreateUnique(), Now.AddHours(-3));
        var found = await AddEditionReportAsync(fixture, Isbn, UserId.CreateUnique(), Now.AddHours(-2));
        var cancelled = await AddEditionReportAsync(fixture, Isbn, UserId.CreateUnique(), Now.AddHours(-1));
        found.MarkFound(UserId.CreateUnique(), "Retrouvé", Now);
        cancelled.Cancel(Now);
        await fixture.Context.SaveChangesAsync();

        var result = await QueueHandler(fixture).Handle(
            new GetNotFoundReportQueueQuery("all", "most-reported", 1, 20, false), default);

        result.IsError.Should().BeFalse();
        result.Value.OpenReportCount.Should().Be(1);
        result.Value.Items.Should().ContainSingle();
        result.Value.Items[0].ReportCount.Should().Be(1);
    }

    [Fact]
    public async Task Queue_NeverExposesMemberIdentity()
    {
        await using var fixture = await MemberSelectionFixture.CreateAsync(Now);
        await fixture.AddBookAsync(Isbn);
        await AddEditionReportAsync(fixture, Isbn, UserId.CreateUnique(), Now.AddHours(-1), "À vérifier");

        var result = await QueueHandler(fixture).Handle(
            new GetNotFoundReportQueueQuery("all", "most-reported", 1, 20, false), default);

        result.IsError.Should().BeFalse();
        var propertyNames = new[]
            {
                typeof(NotFoundReportQueueResult),
                typeof(NotFoundReportTargetResult),
                typeof(NotFoundReportCommentResult)
            }
            .SelectMany(type => type.GetProperties())
            .Select(property => property.Name)
            .ToArray();
        propertyNames.Should().NotContain("UserId");
        propertyNames.Should().NotContain("MemberName");
        propertyNames.Should().NotContain("Email");
        propertyNames.Should().NotContain("ExternalId");
    }

    [Fact]
    public async Task Queue_RareOnly_ReturnsOnlyRareTargets()
    {
        await using var fixture = await MemberSelectionFixture.CreateAsync(Now);
        await fixture.AddBookAsync(Isbn);
        var rareBook = await fixture.AddRareBookAsync(published: true);
        await AddEditionReportAsync(fixture, Isbn, UserId.CreateUnique(), Now.AddHours(-2));
        var rareReport = BookNotFoundReport.CreateForRareBook(
            Guid.NewGuid(), UserId.CreateUnique(), rareBook.Id, null, "Rayon rare", Now.AddHours(-1));
        fixture.Context.BookNotFoundReports.Add(rareReport);
        await fixture.Context.SaveChangesAsync();

        var result = await QueueHandler(fixture).Handle(
            new GetNotFoundReportQueueQuery("all", "most-reported", 1, 20, true), default);

        result.IsError.Should().BeFalse();
        result.Value.Items.Should().ContainSingle();
        result.Value.Items[0].Kind.Should().Be("rare");
        result.Value.Items[0].RareBookId.Should().Be(rareBook.Id.Value);
    }

    private static GetNotFoundReportQueueQueryHandler QueueHandler(MemberSelectionFixture fixture) =>
        new(fixture.Context, fixture.Clock);

    internal static async Task<BookNotFoundReport> AddEditionReportAsync(
        MemberSelectionFixture fixture,
        string isbn,
        UserId userId,
        DateTime reportedAt,
        string? comment = null)
    {
        var report = BookNotFoundReport.CreateForEdition(
            Guid.NewGuid(), userId, ParseIsbn(isbn), null, comment, reportedAt);
        fixture.Context.BookNotFoundReports.Add(report);
        await fixture.Context.SaveChangesAsync();
        return report;
    }

    private static Vole_Papillon_Damour.Domain.BookAggregate.ValueObjects.Isbn13 ParseIsbn(string value) =>
        Vole_Papillon_Damour.Domain.BookAggregate.ValueObjects.Isbn13.TryCreate(value, out var isbn)
            ? isbn
            : throw new InvalidOperationException($"Invalid test ISBN: {value}");
}

public sealed class GetClosedNotFoundReportsQueryHandlerTests
{
    private static readonly DateTime Now = new(2026, 9, 24, 14, 0, 0, DateTimeKind.Utc);
    private const string Isbn = "9782070612758";

    [Fact]
    public async Task Closed_GroupsOneLinePerClosure()
    {
        await using var fixture = await MemberSelectionFixture.CreateAsync(Now);
        await fixture.AddBookAsync(Isbn);
        var volunteerId = UserId.CreateUnique();
        fixture.Context.Users.Add(User.CreateFromExternalIdentity(
            volunteerId,
            $"volunteer-{Guid.NewGuid():N}",
            "volunteer@example.test",
            Now,
            new Name("Clara", "Bénévole")));

        var first = await GetNotFoundReportQueueQueryHandlerTests.AddEditionReportAsync(
            fixture, Isbn, UserId.CreateUnique(), Now.AddHours(-3));
        var second = await GetNotFoundReportQueueQueryHandlerTests.AddEditionReportAsync(
            fixture, Isbn, UserId.CreateUnique(), Now.AddHours(-2));
        first.MarkFound(volunteerId, "Retrouvé en rayon", Now);
        second.MarkFound(volunteerId, "Retrouvé en rayon", Now);
        await fixture.Context.SaveChangesAsync();

        var result = await new GetClosedNotFoundReportsQueryHandler(fixture.Context, fixture.Clock)
            .Handle(new GetClosedNotFoundReportsQuery(1, 20, false), default);

        result.IsError.Should().BeFalse();
        result.Value.Items.Should().ContainSingle();
        result.Value.Items[0].Kind.Should().Be("edition");
        result.Value.Items[0].ReportCount.Should().Be(2);
        result.Value.Items[0].Outcome.Should().Be("Found");
        result.Value.Items[0].ClosedByName.Should().Be("Clara Bénévole");
        result.Value.Items[0].Note.Should().Be("Retrouvé en rayon");
    }
}

public sealed class GetNotFoundReportSummaryQueryHandlerTests
{
    private static readonly DateTime Now = new(2026, 9, 24, 14, 0, 0, DateTimeKind.Utc);
    private const string Isbn = "9782070612758";
    private const string OtherIsbn = "9782070584628";

    [Fact]
    public async Task Summary_CountsOpenAndOverdueTargets()
    {
        await using var fixture = await MemberSelectionFixture.CreateAsync(Now);
        await fixture.AddBookAsync(Isbn);
        await fixture.AddBookAsync(OtherIsbn);
        await GetNotFoundReportQueueQueryHandlerTests.AddEditionReportAsync(
            fixture, Isbn, UserId.CreateUnique(), Now.AddDays(-8));
        await GetNotFoundReportQueueQueryHandlerTests.AddEditionReportAsync(
            fixture, Isbn, UserId.CreateUnique(), Now.AddDays(-1));
        await GetNotFoundReportQueueQueryHandlerTests.AddEditionReportAsync(
            fixture, OtherIsbn, UserId.CreateUnique(), Now.AddHours(-1));

        var result = await new GetNotFoundReportSummaryQueryHandler(fixture.Context, fixture.Clock)
            .Handle(new GetNotFoundReportSummaryQuery(null, null, false), default);

        result.IsError.Should().BeFalse();
        result.Value.OpenTargetCount.Should().Be(2);
        result.Value.OverdueTargetCount.Should().Be(1);
    }

    [Fact]
    public async Task Summary_ForIsbn_ReturnsCountAndLatestComment()
    {
        await using var fixture = await MemberSelectionFixture.CreateAsync(Now);
        await fixture.AddBookAsync(Isbn);
        await GetNotFoundReportQueueQueryHandlerTests.AddEditionReportAsync(
            fixture, Isbn, UserId.CreateUnique(), Now.AddDays(-2), "Premier commentaire");
        await GetNotFoundReportQueueQueryHandlerTests.AddEditionReportAsync(
            fixture, Isbn, UserId.CreateUnique(), Now.AddDays(-1), "Commentaire récent");

        var result = await new GetNotFoundReportSummaryQueryHandler(fixture.Context, fixture.Clock)
            .Handle(new GetNotFoundReportSummaryQuery(Isbn, null, false), default);

        result.IsError.Should().BeFalse();
        result.Value.OpenReportCount.Should().Be(2);
        result.Value.FirstReportedAt.Should().Be(new DateTimeOffset(Now.AddDays(-2), TimeSpan.Zero));
        result.Value.LatestComment.Should().NotBeNull();
        result.Value.LatestComment!.Text.Should().Be("Commentaire récent");
    }
}
