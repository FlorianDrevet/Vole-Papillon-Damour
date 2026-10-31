using System.Net;
using System.Net.Http.Headers;
using System.Text;
using Microsoft.Extensions.DependencyInjection;
using ShopAppVpd.Apis;

namespace ShopAppVpd.Tests;

public sealed class VpdApiServiceCollectionExtensionsTests
{
    [Fact]
    public async Task RegistersPublicProductsApiWithoutAuthorizationHeader()
    {
        var requestHandler = new RequestCaptureHandler();
        var services = new ServiceCollection();
        services
            .AddUnauthenticatedVpdApi(new Uri("https://api.example.test/"))
            .ConfigurePrimaryHttpMessageHandler(() => requestHandler);

        using var provider = services.BuildServiceProvider();
        var api = provider.GetRequiredService<IVpdApi>();

        var response = await api.GetProductsAsync(CancellationToken.None);

        Assert.True(response.IsSuccessful);
        Assert.Equal(HttpMethod.Get, requestHandler.Method);
        Assert.Equal(new Uri("https://api.example.test/product"), requestHandler.RequestUri);
        Assert.Null(requestHandler.Authorization);
    }

    private sealed class RequestCaptureHandler : HttpMessageHandler
    {
        public HttpMethod? Method { get; private set; }
        public Uri? RequestUri { get; private set; }
        public AuthenticationHeaderValue? Authorization { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            Method = request.Method;
            RequestUri = request.RequestUri;
            Authorization = request.Headers.Authorization;

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("[]", Encoding.UTF8, "application/json")
            });
        }
    }
}
