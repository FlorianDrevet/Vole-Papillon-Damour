using System.Net.Http.Headers;
using FluentAssertions;
using Vole_Papillon_Damour.Infrastructure.Services.Isbndb;

namespace Vole_Papillon_Damour.Infrastructure.tests.Isbndb;

public sealed class IsbndbRateLimitHeaderTests
{
    [Fact]
    public void TryParse_WhenDailyQuotaAndPolicyArePresent_ReadsLimitRemainingAndReset()
    {
        var now = new DateTime(2026, 10, 23, 12, 0, 0, DateTimeKind.Utc);
        var headers = new HttpResponseMessage().Headers;
        headers.TryAddWithoutValidation("ratelimit", "\"rate\";r=59;t=0, \"daily\";r=4997;t=15025");
        headers.TryAddWithoutValidation("ratelimit-policy", "\"daily\";q=5000;w=86400");

        var parsed = IsbndbRateLimitHeader.TryParse(headers, now, out var result);

        parsed.Should().BeTrue();
        result.Remaining.Should().Be(4_997);
        result.Limit.Should().Be(5_000);
        result.ResetsAt.Should().Be(now.AddSeconds(15_025));
    }

    [Fact]
    public void TryParse_WhenHeaderIsMalformed_ReturnsFalse()
    {
        var headers = new HttpResponseMessage().Headers;
        headers.TryAddWithoutValidation("ratelimit", "not a structured value");
        headers.TryAddWithoutValidation("ratelimit-policy", "\"daily\";q=nope;w=86400");

        IsbndbRateLimitHeader.TryParse(
            headers,
            new DateTime(2026, 10, 23, 12, 0, 0, DateTimeKind.Utc),
            out _).Should().BeFalse();
    }
}
