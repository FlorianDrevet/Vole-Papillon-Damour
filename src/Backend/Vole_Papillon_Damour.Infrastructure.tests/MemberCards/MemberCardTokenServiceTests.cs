using System.Buffers.Text;
using FluentAssertions;
using Microsoft.Extensions.Options;
using Vole_Papillon_Damour.Infrastructure.Services.MemberCards;

namespace Vole_Papillon_Damour.Infrastructure.tests.MemberCards;

public sealed class MemberCardTokenServiceTests
{
    private static MemberCardTokenService Create(string key = "MDEyMzQ1Njc4OWFiY2RlZjAxMjM0NTY3ODlhYmNkZWY=") =>
        new(Options.Create(new MemberCardTokenOptions { SigningKey = key }));

    [Fact]
    public void RoundTrip_ReturnsCardAndVersion()
    {
        var service = Create();
        var cardId = Guid.NewGuid();

        var token = service.Create(cardId, 3);

        token.Should().StartWith("VPDC1.");
        service.TryRead(token, out var readId, out var version).Should().BeTrue();
        readId.Should().Be(cardId);
        version.Should().Be(3);
    }

    [Fact]
    public void TryRead_RejectsTamperedPayload()
    {
        var service = Create();
        var token = service.Create(Guid.NewGuid(), 1);
        var parts = token.Split('.');
        var forged = $"{parts[0]}.{Base64Url.EncodeToString(new byte[20])}.{parts[2]}";

        service.TryRead(forged, out _, out _).Should().BeFalse();
    }

    [Fact]
    public void TryRead_RejectsTokenSignedWithAnotherKey()
    {
        var token = Create("ZmVkY2JhOTg3NjU0MzIxMGZlZGNiYTk4NzY1NDMyMTA=").Create(Guid.NewGuid(), 1);

        Create().TryRead(token, out _, out _).Should().BeFalse();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("9782070612758")]
    [InlineData("VPDC1.")]
    [InlineData("VPDC1.a.b.c")]
    [InlineData("VPDC2.AAAA.AAAA")]
    public void TryRead_RejectsMalformedInput(string? token)
    {
        Create().TryRead(token, out _, out _).Should().BeFalse();
    }

    [Fact]
    public void Token_DoesNotContainTheCardIdInClearText()
    {
        var cardId = Guid.NewGuid();

        Create().Create(cardId, 1).Should().NotContain(cardId.ToString("N")).And.NotContain(cardId.ToString());
    }
}
