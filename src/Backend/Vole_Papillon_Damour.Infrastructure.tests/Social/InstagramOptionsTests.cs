using FluentAssertions;
using Vole_Papillon_Damour.Infrastructure.Services.Social;

namespace Vole_Papillon_Damour.Infrastructure.tests.Social;

public sealed class InstagramOptionsTests
{
    [Fact]
    public void IsAccessTokenNearExpiry_IsTrueFromFortyFiveDaysAfterIssuance()
    {
        var issuedAt = new DateTimeOffset(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);
        var options = new InstagramOptions
        {
            AccessTokenIssuedAt = issuedAt,
        };

        options.IsAccessTokenNearExpiry(issuedAt.AddDays(45)).Should().BeTrue();
        options.IsAccessTokenNearExpiry(issuedAt.AddDays(44).AddHours(23)).Should().BeFalse();
    }

    [Fact]
    public void IsAccessTokenNearExpiry_IsFalseWhenIssuanceDateIsNotConfigured()
    {
        new InstagramOptions()
            .IsAccessTokenNearExpiry(DateTimeOffset.UtcNow)
            .Should().BeFalse();
    }
}
