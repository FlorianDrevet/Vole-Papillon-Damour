using FluentAssertions;
using Vole_Papillon_Damour.Domain.BookAggregate.ValueObjects;
using Vole_Papillon_Damour.Domain.BookMovementAggregate.ValueObjects;
using Vole_Papillon_Damour.Domain.NotFoundReportAggregate;
using Vole_Papillon_Damour.Domain.NotFoundReportAggregate.ValueObjects;
using Vole_Papillon_Damour.Domain.RareBookAggregate.ValueObjects;
using Vole_Papillon_Damour.Domain.UserAggregate.ValueObjects;

namespace Vole_Papillon_Damour.Domain.tests.NotFoundReport;

public sealed class BookNotFoundReportTests
{
    private static readonly UserId Member = UserId.Create(Guid.Parse("00000000-0000-0000-0000-0000000000a1"));
    private static readonly UserId Volunteer = UserId.Create(Guid.Parse("00000000-0000-0000-0000-0000000000b2"));
    private static readonly DateTime ReportedAt = new(2026, 9, 24, 10, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime ClosedAt = new(2026, 9, 24, 11, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void CreateForEdition_StartsOpenWithTrimmedComment()
    {
        Isbn13.TryCreate("9782070612758", out var isbn).Should().BeTrue();

        var report = BookNotFoundReport.CreateForEdition(
            Guid.Parse("00000000-0000-0000-0000-000000000001"),
            Member,
            isbn,
            NotFoundReportLocation.Fair,
            "  Rayon polar vide  ",
            ReportedAt);

        report.Status.Should().Be(NotFoundReportStatus.Open);
        report.IsOpen.Should().BeTrue();
        report.UserId.Should().Be(Member);
        report.Isbn13.Should().Be(isbn);
        report.RareBookId.Should().BeNull();
        report.Location.Should().Be(NotFoundReportLocation.Fair);
        report.Comment.Should().Be("Rayon polar vide");
        report.ReportedAt.Should().Be(ReportedAt);
        report.ClosedAt.Should().BeNull();
    }

    [Fact]
    public void Create_WithBlankComment_StoresNull()
    {
        Isbn13.TryCreate("9782070612758", out var isbn);

        var report = BookNotFoundReport.CreateForEdition(
            Guid.Parse("00000000-0000-0000-0000-000000000002"), Member, isbn, null, "  \t  ", ReportedAt);

        report.Comment.Should().BeNull();
    }

    [Fact]
    public void Create_WithCommentLongerThan280_Throws()
    {
        Isbn13.TryCreate("9782070612758", out var isbn);

        var act = () => BookNotFoundReport.CreateForEdition(
            Guid.Parse("00000000-0000-0000-0000-000000000003"), Member, isbn, null, new string('x', 281), ReportedAt);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Create_WithNonUtcDate_Throws()
    {
        Isbn13.TryCreate("9782070612758", out var isbn);

        var act = () => BookNotFoundReport.CreateForEdition(
            Guid.Parse("00000000-0000-0000-0000-000000000004"),
            Member,
            isbn,
            null,
            null,
            DateTime.SpecifyKind(ReportedAt, DateTimeKind.Local));

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Cancel_FromOpen_SetsCancelledAndClosedAt()
    {
        var report = CreateEditionReport();

        report.Cancel(ClosedAt);

        report.Status.Should().Be(NotFoundReportStatus.Cancelled);
        report.IsOpen.Should().BeFalse();
        report.ClosedAt.Should().Be(ClosedAt);
        report.ClosedBy.Should().BeNull();
    }

    [Fact]
    public void MarkFound_FromOpen_SetsFoundAndAuthor()
    {
        var report = CreateEditionReport();

        report.MarkFound(Volunteer, "Rangé en poésie", ClosedAt);

        report.Status.Should().Be(NotFoundReportStatus.Found);
        report.ClosedBy.Should().Be(Volunteer);
        report.ClosureNote.Should().Be("Rangé en poésie");
        report.ClosedAt.Should().Be(ClosedAt);
    }

    [Fact]
    public void MarkWithdrawn_WithoutNote_Throws()
    {
        var report = CreateEditionReport();

        var act = () => report.MarkWithdrawn(
            Volunteer,
            NotFoundWithdrawalReason.NotFoundOnShelf,
            1,
            BookMovementId.Create(Guid.Parse("00000000-0000-0000-0000-000000000011")),
            "  ",
            ClosedAt);

        act.Should().Throw<ArgumentException>();
    }

    [Theory]
    [InlineData("cancel")]
    [InlineData("found")]
    [InlineData("withdrawn")]
    [InlineData("dismissed")]
    [InlineData("lapsed")]
    public void AnyTransition_FromClosedState_Throws(string transition)
    {
        var report = CreateEditionReport();
        report.MarkFound(Volunteer, null, ClosedAt);

        Action act = transition switch
        {
            "cancel" => () => report.Cancel(ClosedAt.AddHours(1)),
            "found" => () => report.MarkFound(Volunteer, null, ClosedAt.AddHours(1)),
            "withdrawn" => () => report.MarkWithdrawn(
                Volunteer,
                NotFoundWithdrawalReason.Other,
                1,
                BookMovementId.Create(Guid.Parse("00000000-0000-0000-0000-000000000012")),
                "Vérifié",
                ClosedAt.AddHours(1)),
            "dismissed" => () => report.Dismiss(Volunteer, "Doublon", ClosedAt.AddHours(1)),
            "lapsed" => () => report.Lapse(ClosedAt.AddHours(1)),
            _ => throw new ArgumentOutOfRangeException(nameof(transition), transition, null)
        };

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Lapse_FromOpen_SetsLapsedWithoutAuthor()
    {
        var report = CreateEditionReport();

        report.Lapse(ClosedAt);

        report.Status.Should().Be(NotFoundReportStatus.Lapsed);
        report.ClosedAt.Should().Be(ClosedAt);
        report.ClosedBy.Should().BeNull();
    }

    [Fact]
    public void DetachMember_ClearsUserAndComment_KeepsTargetAndStatus()
    {
        var rareBookId = RareBookId.Create(Guid.Parse("00000000-0000-0000-0000-000000000021"));
        var report = BookNotFoundReport.CreateForRareBook(
            Guid.Parse("00000000-0000-0000-0000-000000000022"),
            Member,
            rareBookId,
            NotFoundReportLocation.Premises,
            "  Étagère 4  ",
            ReportedAt);

        report.DetachMember();

        report.UserId.Should().BeNull();
        report.Comment.Should().BeNull();
        report.RareBookId.Should().Be(rareBookId);
        report.Isbn13.Should().BeNull();
        report.Status.Should().Be(NotFoundReportStatus.Open);
    }

    private static BookNotFoundReport CreateEditionReport()
    {
        Isbn13.TryCreate("9782070612758", out var isbn);

        return BookNotFoundReport.CreateForEdition(
            Guid.Parse("00000000-0000-0000-0000-000000000031"), Member, isbn, null, "Commentaire", ReportedAt);
    }
}
