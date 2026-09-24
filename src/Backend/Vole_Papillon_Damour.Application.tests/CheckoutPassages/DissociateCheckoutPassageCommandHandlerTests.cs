using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Vole_Papillon_Damour.Application.CheckoutPassages.Commands.DissociateCheckoutPassage;
using Vole_Papillon_Damour.Application.CheckoutPassages.Queries.LookupCheckoutPassage;
using Vole_Papillon_Damour.Domain.CheckoutPassageAggregate;
using Vole_Papillon_Damour.Domain.CheckoutPassageAggregate.ValueObjects;

namespace Vole_Papillon_Damour.Application.tests.CheckoutPassages;

public sealed class DissociateCheckoutPassageCommandHandlerTests
{
    private static readonly DateTime Now = new(2026, 9, 24, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task Dissociate_HidesPassageFromMemberAndKeepsLines()
    {
        await using var fixture = await CheckoutPassageFixture.CreateAsync(Now);
        var member = await fixture.AddMemberAsync();
        var passage = await fixture.AddAssociatedPassageWithSaleAsync(member, "9782070612758");
        var lineCountBefore = await fixture.Context.CheckoutPassageLines.CountAsync();
        var movementCountBefore = await fixture.Context.BookMovements.CountAsync();

        var result = await CreateHandler(fixture, Now).Handle(
            new DissociateCheckoutPassageCommand(passage.Id, fixture.VolunteerId, "Carte associée par erreur"),
            default);

        result.IsError.Should().BeFalse();
        var reloaded = await fixture.Context.CheckoutPassages.SingleAsync(item => item.Id == passage.Id);
        reloaded.IsVisibleToMember.Should().BeFalse();
        reloaded.UserId.Should().BeNull();
        reloaded.Status.Should().Be(CheckoutPassageStatus.Dissociated);
        (await fixture.Context.CheckoutPassageLines.CountAsync()).Should().Be(lineCountBefore);
        (await fixture.Context.BookMovements.CountAsync()).Should().Be(movementCountBefore);
    }

    [Fact]
    public async Task Dissociate_IsIdempotent()
    {
        await using var fixture = await CheckoutPassageFixture.CreateAsync(Now);
        var member = await fixture.AddMemberAsync();
        var passage = await fixture.AddAssociatedPassageWithSaleAsync(member, "9782070612758");
        var command = new DissociateCheckoutPassageCommand(passage.Id, fixture.VolunteerId, "Correction initiale");

        (await CreateHandler(fixture, Now).Handle(command, default)).IsError.Should().BeFalse();
        var firstDissociatedAt = (await fixture.Context.CheckoutPassages.SingleAsync()).DissociatedAt;
        (await CreateHandler(fixture, Now.AddMinutes(5)).Handle(
            command with {Reason = "Nouvelle demande"}, default)).IsError.Should().BeFalse();

        var reloaded = await fixture.Context.CheckoutPassages.SingleAsync(item => item.Id == passage.Id);
        reloaded.Status.Should().Be(CheckoutPassageStatus.Dissociated);
        reloaded.DissociatedAt.Should().Be(firstDissociatedAt);
        reloaded.DissociatedByUserId.Should().Be(fixture.VolunteerId);
    }

    [Fact]
    public async Task Dissociate_WithoutReason_ReturnsValidationError()
    {
        var command = new DissociateCheckoutPassageCommand(Guid.NewGuid(),
            Vole_Papillon_Damour.Domain.UserAggregate.ValueObjects.UserId.Create(Guid.NewGuid()), "  ");

        var result = await new DissociateCheckoutPassageCommandValidator().ValidateAsync(command);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(error => error.PropertyName == nameof(command.Reason));
    }

    [Fact]
    public async Task Lookup_WithAmbiguousPrefix_ReturnsConflict()
    {
        await using var fixture = await CheckoutPassageFixture.CreateAsync(Now);
        var member = await fixture.AddMemberAsync();
        var first = CheckoutPassage.OpenFromSale(
            Guid.Parse("3f2a9c1b-0000-0000-0000-000000000001"), Now, Now);
        var second = CheckoutPassage.OpenFromSale(
            Guid.Parse("3f2a9c1b-0000-0000-0000-000000000002"), Now.AddMinutes(-1), Now);
        first.Associate(member.Id, fixture.VolunteerId, Now);
        second.Associate(member.Id, fixture.VolunteerId, Now);
        fixture.Context.CheckoutPassages.AddRange(first, second);
        await fixture.Context.SaveChangesAsync();

        var sql = fixture.Context.CheckoutPassages
            .Where(item => item.Status == CheckoutPassageStatus.Associated && item.OccurredAt >= Now.AddDays(-90))
            .ToQueryString();
        var result = await new LookupCheckoutPassageQueryHandler(
            fixture.Context, new CheckoutPassageTestClock(Now)).Handle(
            new LookupCheckoutPassageQuery("3F2A9C1B"), default);

        sql.Should().Contain("WHERE");
        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("CheckoutPassage.AmbiguousReference");
    }

    [Fact]
    public async Task Lookup_WithShortReferenceReturnsMemberNameAndLineCount()
    {
        await using var fixture = await CheckoutPassageFixture.CreateAsync(Now);
        var member = await fixture.AddMemberWithCardAsync("Camille");
        var passage = await fixture.AddAssociatedPassageWithSaleAsync(member.User, "9782070612758");
        var reference = passage.Id.ToString("N")[..8].ToUpperInvariant();

        var result = await new LookupCheckoutPassageQueryHandler(
            fixture.Context, new CheckoutPassageTestClock(Now)).Handle(
            new LookupCheckoutPassageQuery(reference), default);

        result.IsError.Should().BeFalse();
        result.Value.Id.Should().Be(passage.Id);
        result.Value.OccurredAt.Should().Be(Now);
        result.Value.LineCount.Should().Be(1);
        result.Value.DisplayLabel.Should().Be("Camille");
    }

    private static DissociateCheckoutPassageCommandHandler CreateHandler(
        CheckoutPassageFixture fixture,
        DateTime now) => new(
        fixture.Context,
        new CheckoutPassageTestClock(now),
        NullLogger<DissociateCheckoutPassageCommandHandler>.Instance);
}
