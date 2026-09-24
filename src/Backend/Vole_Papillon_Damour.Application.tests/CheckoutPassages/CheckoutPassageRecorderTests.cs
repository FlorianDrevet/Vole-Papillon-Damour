using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Vole_Papillon_Damour.Application;
using Vole_Papillon_Damour.Application.CheckoutPassages.Common;
using Vole_Papillon_Damour.Domain.CheckoutPassageAggregate;
using Vole_Papillon_Damour.Domain.MemberSelectionAggregate.ValueObjects;

namespace Vole_Papillon_Damour.Application.tests.CheckoutPassages;

public sealed class CheckoutPassageRecorderTests
{
    private static readonly DateTime Now = new(2026, 9, 24, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task RecordOrdinarySale_OpensPassageOnceAndAddsOneLinePerMovement()
    {
        await using var fixture = await CheckoutPassageFixture.CreateAsync(Now);
        var passageId = Guid.NewGuid();
        var (book, movement1) = await fixture.AddSaleAsync("9782070612758", passageId);
        var (_, movement2) = await fixture.AddSaleAsync("9782070612758", passageId);
        var recorder = fixture.CreateRecorder();

        await recorder.RecordOrdinarySaleAsync(passageId, movement1, book, "9782070612758", default);
        await recorder.RecordOrdinarySaleAsync(passageId, movement2, book, "9782070612758", default);
        await recorder.RecordOrdinarySaleAsync(passageId, movement1, book, "9782070612758", default);

        (await fixture.Context.CheckoutPassages.CountAsync()).Should().Be(0);
        (await fixture.Context.CheckoutPassageLines.CountAsync()).Should().Be(0);
        await fixture.Context.SaveChangesAsync();

        (await fixture.Context.CheckoutPassages.CountAsync()).Should().Be(1);
        (await fixture.Context.CheckoutPassageLines.CountAsync()).Should().Be(2);
    }

    [Fact]
    public async Task MarkSelectionPurchased_MatchesExactIsbnOnly()
    {
        await using var fixture = await CheckoutPassageFixture.CreateAsync(Now);
        var member = await fixture.AddMemberAsync();
        var sameEdition = await fixture.AddSelectionAsync(member, "9782070612758");
        var otherEditionSameWork = await fixture.AddSelectionAsync(member, "9782070584628");
        var passage = await fixture.AddAssociatedPassageWithSaleAsync(member, "9782070612758");

        await fixture.CreateRecorder().MarkSelectionPurchasedAsync(passage, default);
        await fixture.Context.SaveChangesAsync();

        (await fixture.Reload(sameEdition)).Status.Should().Be(MemberSelectionStatus.Purchased);
        (await fixture.Reload(otherEditionSameWork)).Status.Should().Be(MemberSelectionStatus.ToTake);
    }

    [Fact]
    public async Task MarkSelectionPurchased_MarksSelectionOnRequestedAndCanonicalIsbn()
    {
        await using var fixture = await CheckoutPassageFixture.CreateAsync(Now);
        var member = await fixture.AddMemberAsync();
        var onOldIsbn = await fixture.AddSelectionAsync(member, "9782070612758");
        await fixture.RedirectBookAsync(from: "9782070612758", to: "9782070584628");
        var passage = await fixture.AddAssociatedPassageWithSaleAsync(member, requestedIsbn: "9782070612758");

        await fixture.CreateRecorder().MarkSelectionPurchasedAsync(passage, default);
        await fixture.Context.SaveChangesAsync();

        (await fixture.Reload(onOldIsbn)).Status.Should().Be(MemberSelectionStatus.Purchased);
    }

    [Fact]
    public async Task MarkSelectionPurchased_IgnoresVoidedLinesAndOtherMembers()
    {
        await using var fixture = await CheckoutPassageFixture.CreateAsync(Now);
        var member = await fixture.AddMemberAsync();
        var other = await fixture.AddMemberAsync();
        var otherSelection = await fixture.AddSelectionAsync(other, "9782070612758");
        var voidedSelection = await fixture.AddSelectionAsync(member, "9782253006329");
        var passage = await fixture.AddAssociatedPassageWithSaleAsync(member, "9782070612758");
        await fixture.AddVoidedLineAsync(passage, "9782253006329");

        await fixture.CreateRecorder().MarkSelectionPurchasedAsync(passage, default);
        await fixture.Context.SaveChangesAsync();

        (await fixture.Reload(otherSelection)).Status.Should().Be(MemberSelectionStatus.ToTake);
        (await fixture.Reload(voidedSelection)).Status.Should().Be(MemberSelectionStatus.ToTake);
    }

    [Fact]
    public async Task MarkSelectionPurchased_WhenPassageNotAssociated_DoesNothing()
    {
        await using var fixture = await CheckoutPassageFixture.CreateAsync(Now);
        var member = await fixture.AddMemberAsync();
        var selection = await fixture.AddSelectionAsync(member, "9782070612758");
        var passage = await fixture.AddPendingPassageWithSaleAsync("9782070612758");

        await fixture.CreateRecorder().MarkSelectionPurchasedAsync(passage, default);
        await fixture.Context.SaveChangesAsync();

        (await fixture.Reload(selection)).Status.Should().Be(MemberSelectionStatus.ToTake);
    }

    [Fact]
    public async Task RecordRareSale_IsIdempotentPerRareBook()
    {
        await using var fixture = await CheckoutPassageFixture.CreateAsync(Now);
        var member = await fixture.AddMemberAsync();
        var rareBook = fixture.CreateRareBook(member.Id);
        var selection = await fixture.AddSelectionForRareBookAsync(member, rareBook);
        var passageId = Guid.NewGuid();
        var passage = CheckoutPassage.OpenFromSale(passageId, Now, Now);
        passage.Associate(member.Id, member.Id, Now);
        fixture.Context.CheckoutPassages.Add(passage);
        await fixture.Context.SaveChangesAsync();
        var recorder = fixture.CreateRecorder();

        await recorder.RecordRareSaleAsync(passageId, rareBook, Now, null, default);
        await recorder.RecordRareSaleAsync(passageId, rareBook, Now, null, default);
        await fixture.Context.SaveChangesAsync();

        (await fixture.Context.CheckoutPassages.CountAsync()).Should().Be(1);
        var line = await fixture.Context.CheckoutPassageLines.SingleAsync();
        line.RareBookId.Should().Be(rareBook.Id);
        (await fixture.Reload(selection)).Status.Should().Be(MemberSelectionStatus.Purchased);
    }

    [Fact]
    public async Task RecordOrdinarySale_WhenMovementLineAlreadyExists_DoesNotDuplicateAfterReload()
    {
        await using var fixture = await CheckoutPassageFixture.CreateAsync(Now);
        var passageId = Guid.NewGuid();
        var (book, movement) = await fixture.AddSaleAsync("9782070612758", passageId);
        var recorder = fixture.CreateRecorder();

        await recorder.RecordOrdinarySaleAsync(passageId, movement, book, "9782070612758", default);
        await fixture.Context.SaveChangesAsync();
        fixture.Context.ChangeTracker.Clear();

        await recorder.RecordOrdinarySaleAsync(passageId, movement, book, "9782070612758", default);
        await fixture.Context.SaveChangesAsync();

        (await fixture.Context.CheckoutPassageLines.CountAsync()).Should().Be(1);
    }

    [Fact]
    public void AddApplication_RegistersCheckoutPassageRecorderAsScoped()
    {
        var services = new ServiceCollection();

        services.AddApplication();

        services.Should().ContainSingle(descriptor =>
            descriptor.ServiceType == typeof(CheckoutPassageRecorder)
            && descriptor.ImplementationType == typeof(CheckoutPassageRecorder)
            && descriptor.Lifetime == ServiceLifetime.Scoped);
    }
}
