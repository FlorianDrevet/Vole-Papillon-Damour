using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Vole_Papillon_Damour.Application.MemberCards.Commands.RotateMyCard;
using Vole_Papillon_Damour.Application.MemberCards.Queries.GetMyCard;

namespace Vole_Papillon_Damour.Application.tests.MemberCards;

public sealed class MemberCardHandlersTests
{
    private static readonly DateTime Now = new(2026, 9, 23, 10, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task GetMyCard_IssuesOnceAndReturnsSamePayload()
    {
        await using var fixture = await MemberCardFixture.CreateAsync(Now);

        var first = await fixture.CreateGetMyCardHandler().Handle(fixture.MyCardQuery(), default);
        var second = await fixture.CreateGetMyCardHandler().Handle(fixture.MyCardQuery(), default);

        second.Value.QrPayload.Should().Be(first.Value.QrPayload);
        second.Value.RecoveryCode.Should().Be(first.Value.RecoveryCode);
        (await fixture.Context.MemberCards.CountAsync()).Should().Be(1);
    }

    [Fact]
    public async Task GetMyCard_PayloadDoesNotContainEmailOrName()
    {
        await using var fixture = await MemberCardFixture.CreateAsync(Now);

        var card = await fixture.CreateGetMyCardHandler().Handle(
            fixture.MyCardQuery(email: "camille@example.test", firstName: "Camille"),
            default);

        card.Value.QrPayload.Should().NotContainAny("camille", "Camille", "example.test", fixture.ExternalId.ToString());
        card.Value.DisplayLabel.Should().Be("Camille");
    }

    [Fact]
    public async Task Rotate_InvalidatesPreviousQrAndCode()
    {
        await using var fixture = await MemberCardFixture.CreateAsync(Now);
        var before = await fixture.CreateGetMyCardHandler().Handle(fixture.MyCardQuery(), default);

        var after = await fixture.CreateRotateHandler().Handle(fixture.RotateCommand(), default);

        (await fixture.Resolve(before.Value.QrPayload)).FirstError.Code.Should().Be("MemberCard.NotRecognised");
        (await fixture.Resolve(before.Value.RecoveryCode)).FirstError.Code.Should().Be("MemberCard.NotRecognised");
        (await fixture.Resolve(after.Value.QrPayload)).Value.DisplayLabel.Should().Be("Camille");
        (await fixture.Resolve(after.Value.RecoveryCode.ToLowerInvariant().Replace("-", " "))).IsError.Should().BeFalse();
    }

    [Fact]
    public async Task Resolve_WhenUserAnonymised_ReturnsNotRecognised()
    {
        await using var fixture = await MemberCardFixture.CreateAsync(Now);
        var card = await fixture.CreateGetMyCardHandler().Handle(fixture.MyCardQuery(), default);
        await fixture.AnonymiseMemberAsync();

        (await fixture.Resolve(card.Value.QrPayload)).FirstError.Code.Should().Be("MemberCard.NotRecognised");
    }

    [Fact]
    public async Task Resolve_WhenNameMissing_UsesGenericLabel()
    {
        await using var fixture = await MemberCardFixture.CreateAsync(Now);
        var card = await fixture.CreateGetMyCardHandler().Handle(fixture.MyCardQuery(firstName: null), default);

        (await fixture.Resolve(card.Value.QrPayload)).Value.DisplayLabel.Should().Be("Membre");
    }

    [Fact]
    public async Task Issue_WhenRecoveryCodeCollides_RetriesWithAnotherCode()
    {
        await using var fixture = await MemberCardFixture.CreateAsync(Now, randomSequence: [0, 0, 0, 0, 1, 1]);
        await fixture.IssueCardForOtherMemberAsync(); // consumes LUNE-0000 (indices 0,0)

        var card = await fixture.CreateGetMyCardHandler().Handle(fixture.MyCardQuery(), default);

        card.Value.RecoveryCode.Should().NotBe(fixture.OtherMemberRecoveryCode);
    }
}
