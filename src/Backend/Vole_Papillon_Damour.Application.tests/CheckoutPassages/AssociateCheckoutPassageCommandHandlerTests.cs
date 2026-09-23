using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Vole_Papillon_Damour.Application.CheckoutPassages.Commands.AssociateCheckoutPassage;
using Vole_Papillon_Damour.Domain.CheckoutPassageAggregate.ValueObjects;
using Vole_Papillon_Damour.Domain.MemberSelectionAggregate.ValueObjects;

namespace Vole_Papillon_Damour.Application.tests.CheckoutPassages;

public sealed class AssociateCheckoutPassageCommandHandlerTests
{
    private static readonly DateTime Now = new(2026, 9, 24, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task Handle_WhenAssociationArrivesBeforeSales_LinesJoinTheAssociatedPassage()
    {
        await using var fixture = await CheckoutPassageFixture.CreateAsync(Now);
        var member = await fixture.AddMemberWithCardAsync("Camille");
        var passageId = Guid.NewGuid();

        var association = await fixture.CreateAssociateHandler().Handle(
            new AssociateCheckoutPassageCommand(passageId, member.QrPayload, Now, fixture.VolunteerId), default);
        await fixture.RegisterSaleAsync("9782070612758", passageId);
        await fixture.RegisterSaleAsync("9782203001015", passageId);

        association.Value.Status.Should().Be("Associated");
        association.Value.DisplayLabel.Should().Be("Camille");
        var passage = await fixture.Context.CheckoutPassages.SingleAsync();
        passage.UserId.Should().Be(member.UserId);
        (await fixture.Context.CheckoutPassageLines.CountAsync(line => line.CheckoutPassageId == passageId)).Should().Be(2);
    }

    [Fact]
    public async Task Handle_WhenSalesArriveBeforeAssociation_SameResult()
    {
        await using var fixture = await CheckoutPassageFixture.CreateAsync(Now);
        var member = await fixture.AddMemberWithCardAsync("Camille");
        var passageId = Guid.NewGuid();
        await fixture.RegisterSaleAsync("9782070612758", passageId);
        await fixture.RegisterSaleAsync("9782203001015", passageId);

        await fixture.CreateAssociateHandler().Handle(
            new AssociateCheckoutPassageCommand(passageId, member.QrPayload, Now, fixture.VolunteerId), default);

        (await fixture.Context.CheckoutPassages.SingleAsync()).Status.Should().Be(CheckoutPassageStatus.Associated);
        (await fixture.Context.CheckoutPassageLines.CountAsync()).Should().Be(2);
    }

    [Fact]
    public async Task Handle_ReplayedTwice_ProducesOnePassageAndSameLines()
    {
        await using var fixture = await CheckoutPassageFixture.CreateAsync(Now);
        var member = await fixture.AddMemberWithCardAsync("Camille");
        var passageId = Guid.NewGuid();
        var gesture = Guid.NewGuid();
        var command = new AssociateCheckoutPassageCommand(passageId, member.QrPayload, Now, fixture.VolunteerId);

        await fixture.CreateAssociateHandler().Handle(command, default);
        await fixture.RegisterSaleAsync("9782070612758", passageId, gesture);
        await fixture.RegisterSaleAsync("9782070612758", passageId, gesture);
        var replay = await fixture.CreateAssociateHandler().Handle(command, default);

        replay.Value.AlreadyProcessed.Should().BeTrue();
        (await fixture.Context.CheckoutPassages.CountAsync()).Should().Be(1);
        (await fixture.Context.CheckoutPassageLines.CountAsync()).Should().Be(1);
        (await fixture.Context.BookMovements.CountAsync()).Should().Be(1);
    }

    [Fact]
    public async Task Handle_WithRevokedToken_RecordsUnresolvedAndKeepsSalesAnonymous()
    {
        await using var fixture = await CheckoutPassageFixture.CreateAsync(Now);
        var member = await fixture.AddMemberWithCardAsync("Camille");
        var oldPayload = member.QrPayload;
        await fixture.RotateCardAsync(member);
        var passageId = Guid.NewGuid();
        await fixture.RegisterSaleAsync("9782070612758", passageId);

        var result = await fixture.CreateAssociateHandler().Handle(
            new AssociateCheckoutPassageCommand(passageId, oldPayload, Now, fixture.VolunteerId), default);

        result.IsError.Should().BeFalse();
        result.Value.Status.Should().Be("Unresolved");
        result.Value.DisplayLabel.Should().BeNull();
        (await fixture.Context.CheckoutPassages.SingleAsync()).UserId.Should().BeNull();
    }

    [Fact]
    public async Task Handle_WhenPassageAlreadyBelongsToAnotherMember_ReturnsConflictAndChangesNothing()
    {
        await using var fixture = await CheckoutPassageFixture.CreateAsync(Now);
        var camille = await fixture.AddMemberWithCardAsync("Camille");
        var dominique = await fixture.AddMemberWithCardAsync("Dominique");
        var passageId = Guid.NewGuid();
        await fixture.CreateAssociateHandler().Handle(
            new AssociateCheckoutPassageCommand(passageId, camille.QrPayload, Now, fixture.VolunteerId), default);

        var result = await fixture.CreateAssociateHandler().Handle(
            new AssociateCheckoutPassageCommand(passageId, dominique.QrPayload, Now, fixture.VolunteerId), default);

        result.FirstError.Code.Should().Be("CheckoutPassage.AlreadyAssociatedToAnotherMember");
        (await fixture.Context.CheckoutPassages.SingleAsync()).UserId.Should().Be(camille.UserId);
    }

    [Fact]
    public async Task Handle_MarksMatchingSelectionPurchased()
    {
        await using var fixture = await CheckoutPassageFixture.CreateAsync(Now);
        var member = await fixture.AddMemberWithCardAsync("Camille");
        var selected = await fixture.AddSelectionAsync(member.User, "9782070612758");
        var otherEdition = await fixture.AddSelectionAsync(member.User, "9782070584628");
        var passageId = Guid.NewGuid();
        await fixture.RegisterSaleAsync("9782070612758", passageId);

        await fixture.CreateAssociateHandler().Handle(
            new AssociateCheckoutPassageCommand(passageId, member.RecoveryCode, Now, fixture.VolunteerId), default);

        (await fixture.Reload(selected)).Status.Should().Be(MemberSelectionStatus.Purchased);
        (await fixture.Reload(otherEdition)).Status.Should().Be(MemberSelectionStatus.ToTake);
    }
}