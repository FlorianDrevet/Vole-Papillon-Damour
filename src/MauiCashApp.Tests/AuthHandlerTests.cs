using System.Net;
using System.Net.Http.Headers;
using ShopAppVpd.Interfaces;
using ShopAppVpd.Services;

namespace ShopAppVpd.Tests;

public sealed class AuthHandlerTests
{
    [Fact]
    public async Task Adds_the_access_token_as_a_bearer_header()
    {
        var authService = new StubAuthService("api-access-token");
        HttpRequestMessage? receivedRequest = null;

        using var handler = new AuthHandler(authService)
        {
            InnerHandler = new RecordingHandler(request => receivedRequest = request)
        };
        using var client = new HttpClient(handler);

        await client.GetAsync("https://api.example.test/product");

        Assert.NotNull(receivedRequest);
        Assert.Equal(
            new AuthenticationHeaderValue("Bearer", "api-access-token"),
            receivedRequest!.Headers.Authorization);
    }

    private sealed class StubAuthService(string accessToken) : IAuthService
    {
        public Task<string> AcquireAccessTokenAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(accessToken);

        public Task SignOutAsync(CancellationToken cancellationToken = default)
            => Task.CompletedTask;
    }

    private sealed class RecordingHandler(Action<HttpRequestMessage> onRequest) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            onRequest(request);
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK));
        }
    }
}
