using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Vole_Papillon_Damour.Application.MemberSelection.Commands.AddSelectionItem;
using Vole_Papillon_Damour.Application.MemberSelection.Commands.MergeSelection;
using Vole_Papillon_Damour.Application.MemberSelection.Common;
using Vole_Papillon_Damour.Domain.BookAggregate.ValueObjects;

namespace Vole_Papillon_Damour.Application.tests.MemberSelection;

public sealed class MergeSelectionCommandHandlerTests
{
    private static readonly Guid ExternalId = Guid.Parse("7f0b0f0e-0000-0000-0000-00000000c0de");
    private static readonly DateTime Now = new(2026, 9, 23, 10, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task Merge_AddsMissing_KeepsRemote_Deduplicates()
    {
        await using var fixture = await MemberSelectionFixture.CreateAsync(Now);
        await fixture.AddBookAsync("9782070612758");
        await fixture.AddBookAsync("9782070584628");
        await fixture.AddBookAsync("9782253006329");
        await fixture.CreateAddHandler().Handle(Add(isbn: "9782253006329"), default);

        var result = await fixture.CreateMergeHandler().Handle(Merge(
            new MergeSelectionEntry("9782070612758", null, Now.AddDays(-2)),
            new MergeSelectionEntry("9782070584628", null, Now.AddDays(-1)),
            new MergeSelectionEntry("9782070584628", null, Now.AddDays(-1)),
            new MergeSelectionEntry("9782253006329", null, Now.AddDays(-3))), default);

        result.Value.Added.Should().Be(2);
        result.Value.AlreadyPresent.Should().Be(1);
        (await fixture.Context.MemberSelectionItems.CountAsync()).Should().Be(3);
        (await fixture.Context.MemberSelectionItems.SingleAsync(item => item.Isbn13 == Isbn("9782070612758")))
            .AddedAt.Should().Be(Now.AddDays(-2));
    }

    [Fact]
    public async Task Merge_RejectsUnknownWithoutFailingBatch()
    {
        await using var fixture = await MemberSelectionFixture.CreateAsync(Now);
        await fixture.AddBookAsync("9782070612758");

        var result = await fixture.CreateMergeHandler().Handle(Merge(
            new MergeSelectionEntry("9782070612758", null, Now),
            new MergeSelectionEntry("9780000000002", null, Now)), default);

        result.Value.Added.Should().Be(1);
        result.Value.Rejected.Should().Equal("9780000000002");
    }

    [Fact]
    public async Task Merge_ClampsFutureLocalDates()
    {
        await using var fixture = await MemberSelectionFixture.CreateAsync(Now);
        await fixture.AddBookAsync("9782070612758");

        await fixture.CreateMergeHandler().Handle(Merge(
            new MergeSelectionEntry("9782070612758", null, Now.AddYears(1))), default);

        (await fixture.Context.MemberSelectionItems.SingleAsync()).AddedAt.Should().Be(Now);
    }

    [Fact]
    public async Task Merge_WithEmptyBatch_ChangesNothing()
    {
        await using var fixture = await MemberSelectionFixture.CreateAsync(Now);

        var result = await fixture.CreateMergeHandler().Handle(Merge(), default);

        result.Value.Should().BeEquivalentTo(new MergeSelectionResult(0, 0, []));
    }

    [Fact]
    public void Validator_RejectsMoreThanTwoHundredEntries()
    {
        var entries = Enumerable.Repeat(
            new MergeSelectionEntry("9782070612758", null, Now), 201).ToArray();
        var validation = new MergeSelectionCommandValidator().Validate(Merge(entries));

        validation.IsValid.Should().BeFalse();
        validation.Errors.Should().Contain(error => error.PropertyName == "Entries");
    }

    [Theory]
    [InlineData(null, null)]
    [InlineData("9782070612758", "7f0b0f0e-0000-0000-0000-00000000c0de")]
    public void Validator_RequiresExactlyOneTarget(string? isbn, string? rareBookId)
    {
        Guid? parsedRareBookId = rareBookId is null ? null : Guid.Parse(rareBookId);
        var validation = new MergeSelectionCommandValidator().Validate(Merge(
            new MergeSelectionEntry(isbn, parsedRareBookId, Now)));

        validation.IsValid.Should().BeFalse();
        validation.Errors.Should().Contain(error => error.PropertyName.Contains("Entries"));
    }

    private static AddSelectionItemCommand Add(string? isbn) =>
        new(ExternalId, "camille@example.test", "Camille", null, isbn, null);

    private static MergeSelectionCommand Merge(params MergeSelectionEntry[] entries) =>
        new(ExternalId, "camille@example.test", "Camille", null, entries);

    private static Isbn13 Isbn(string value) =>
        Isbn13.TryCreate(value, out var isbn)
            ? isbn
            : throw new InvalidOperationException($"Invalid test ISBN: {value}");
}


