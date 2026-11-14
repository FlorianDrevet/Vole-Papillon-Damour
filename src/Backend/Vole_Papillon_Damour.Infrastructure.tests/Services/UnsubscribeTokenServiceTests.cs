using System.Security.Cryptography;
using System.Text;
using FluentAssertions;
using Microsoft.Extensions.Options;
using Vole_Papillon_Damour.Application.Common.Interfaces.Services;
using Vole_Papillon_Damour.Infrastructure.Services.BookAlerts;

namespace Vole_Papillon_Damour.Infrastructure.tests.Services;

public sealed class UnsubscribeTokenServiceTests
{
    private const string SigningKey = "Ck8+0m1tQZ0v7yq3Nn1sQw2b9hK4lL6pR8tU0wX2yZ4=";
    private static readonly DateTime Now = new(2026, 9, 17, 10, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void TryValidate_AcceptsATokenItJustMintedAndReturnsTheSameMember()
    {
        var memberId = Guid.Parse("2a8def6a-4378-41b1-9166-c12b1c42c5a6");
        var service = CreateService();

        var token = service.Create(memberId);

        service.TryValidate(token, out var validated).Should().BeTrue();
        validated.Should().Be(memberId);
    }

    [Fact]
    public void TryValidate_RejectsATokenWhoseMemberWasSwappedForAnother()
    {
        var service = CreateService();
        var token = service.Create(Guid.Parse("2a8def6a-4378-41b1-9166-c12b1c42c5a6"));
        var forged = service.Create(Guid.Parse("11111111-2222-3333-4444-555555555555"));

        // Keep the victim's payload, bolt on a signature minted for someone else.
        var tampered = string.Concat(
            token.AsSpan(0, token.IndexOf('.')),
            forged.AsSpan(forged.IndexOf('.')));

        service.TryValidate(tampered, out _).Should().BeFalse();
    }

    [Fact]
    public void TryValidate_RejectsATokenSignedWithAnotherKey()
    {
        var memberId = Guid.NewGuid();
        var token = CreateService(signingKey: "sVN9k2Yq0pL4mR7tX1zC3bN6vH8jK5wQ2eR4tY6uI8o=").Create(memberId);

        CreateService().TryValidate(token, out _).Should().BeFalse();
    }

    [Fact]
    public void TryValidate_RejectsATokenPastItsLifetime()
    {
        var memberId = Guid.NewGuid();
        var token = CreateService(lifetimeDays: 30).Create(memberId);

        CreateService(now: Now.AddDays(31)).TryValidate(token, out _).Should().BeFalse();
    }

    [Fact]
    public void TryValidate_RejectsMalformedInputInsteadOfThrowing()
    {
        var service = CreateService();

        service.TryValidate("", out _).Should().BeFalse();
        service.TryValidate("no-separator", out _).Should().BeFalse();
        service.TryValidate("not+base64url.$$$", out _).Should().BeFalse();
    }

    [Fact]
    public void TryValidate_RejectsTokensUsedForTheWrongUnsubscribePurpose()
    {
        var service = CreateService();
        var memberId = Guid.Parse("2a8def6a-4378-41b1-9166-c12b1c42c5a6");
        var alertsToken = service.Create(memberId, UnsubscribePurpose.BookAlerts);
        var digestToken = service.Create(memberId, UnsubscribePurpose.FairDigest);

        service.TryValidate(alertsToken, UnsubscribePurpose.BookAlerts, out var validatedAlerts).Should().BeTrue();
        validatedAlerts.Should().Be(memberId);
        service.TryValidate(digestToken, UnsubscribePurpose.FairDigest, out var validatedDigest).Should().BeTrue();
        validatedDigest.Should().Be(memberId);
        service.TryValidate(digestToken, UnsubscribePurpose.BookAlerts, out _).Should().BeFalse();
        service.TryValidate(alertsToken, UnsubscribePurpose.FairDigest, out _).Should().BeFalse();
    }

    [Fact]
    public void TryValidate_TreatsALegacyTokenWithoutPurposeAsBookAlerts()
    {
        var memberId = Guid.Parse("2a8def6a-4378-41b1-9166-c12b1c42c5a6");
        var legacyToken = CreateLegacyToken(memberId, SigningKey, Now.AddDays(30));
        var service = CreateService();

        service.TryValidate(legacyToken, out var validatedId, out var purpose).Should().BeTrue();

        validatedId.Should().Be(memberId);
        purpose.Should().Be(UnsubscribePurpose.BookAlerts);
    }

    private static IUnsubscribeTokenService CreateService(
        string signingKey = SigningKey,
        int lifetimeDays = 180,
        DateTime? now = null)
    {
        return new UnsubscribeTokenService(
            Options.Create(new UnsubscribeTokenOptions
            {
                SigningKey = signingKey,
                Endpoint = "https://api.example.org/integrations/email/unsubscribe",
                LifetimeDays = lifetimeDays
            }),
            new FixedDateTimeProvider(now ?? Now));
    }

    private static string CreateLegacyToken(Guid memberId, string signingKey, DateTime expiresAt)
    {
        var payload = Encoding.UTF8.GetBytes(FormattableString.Invariant(
            $"{memberId:N}:{new DateTimeOffset(expiresAt, TimeSpan.Zero).ToUnixTimeSeconds()}"));
        var key = Convert.FromBase64String(signingKey);
        var signature = HMACSHA256.HashData(key, payload);
        return $"{Encode(payload)}.{Encode(signature)}";
    }

    private static string Encode(byte[] value) => Convert.ToBase64String(value)
        .TrimEnd('=')
        .Replace('+', '-')
        .Replace('/', '_');

    private sealed class FixedDateTimeProvider(DateTime utcNow) : IDateTimeProvider
    {
        public DateTime UtcNow { get; } = utcNow;
    }
}
