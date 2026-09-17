using FluentAssertions;
using Vole_Papillon_Damour.Infrastructure.Services.BookAlerts;

namespace Vole_Papillon_Damour.Infrastructure.tests.Services;

public sealed class BookAlertEmailHeadersTests
{
    private const string Endpoint = "https://api.example.org/integrations/email/unsubscribe";

    [Fact]
    public void BuildOneClickUnsubscribe_AdvertisesTheSignedUrlAndTheOneClickDirective()
    {
        var headers = BookAlertEmailHeaders.BuildOneClickUnsubscribe(Endpoint, "tok3n.s1g");

        headers[BookAlertEmailHeaders.ListUnsubscribe]
            .Should().Be($"<{Endpoint}?token=tok3n.s1g>");
        headers[BookAlertEmailHeaders.ListUnsubscribePost]
            .Should().Be("List-Unsubscribe=One-Click");
    }

    [Fact]
    public void BuildOneClickUnsubscribe_EscapesTokenCharactersThatWouldBreakTheQueryString()
    {
        var headers = BookAlertEmailHeaders.BuildOneClickUnsubscribe(Endpoint, "a+b/c=d&e");

        headers[BookAlertEmailHeaders.ListUnsubscribe]
            .Should().NotContain("&e")
            .And.Contain("%26e");
    }

    [Theory]
    [InlineData(null, "token")]
    [InlineData("", "token")]
    [InlineData(Endpoint, null)]
    [InlineData(Endpoint, "")]
    public void BuildOneClickUnsubscribe_AdvertisesNothingWhenItCannotBuildAWorkingUrl(
        string? endpoint,
        string? token)
    {
        // Announcing one-click without a usable endpoint is worse than staying
        // silent: providers POST to it and expect the unsubscription to happen.
        BookAlertEmailHeaders.BuildOneClickUnsubscribe(endpoint, token).Should().BeEmpty();
    }
}
