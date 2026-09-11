using System.Net;
using FluentAssertions;
using Vole_Papillon_Damour.Infrastructure.Services.Social;

namespace Vole_Papillon_Damour.Infrastructure.tests.Social;

public sealed class MediaDownloaderTests
{
    [Fact]
    public async Task DownloadAsync_CopiesTheRemoteContentToASeekableStream()
    {
        var handler = new StubHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new ByteArrayContent([1, 2, 3, 4])
        });
        using var httpClient = new HttpClient(handler);
        var downloader = new MediaDownloader(httpClient);

        await using var stream = await downloader.DownloadAsync(
            new Uri("https://cdn.example.test/photo.jpg"),
            CancellationToken.None);

        stream.CanSeek.Should().BeTrue();
        stream.Position.Should().Be(0);
        stream.ReadByte().Should().Be(1);
        handler.LastRequest!.RequestUri.Should().Be(new Uri("https://cdn.example.test/photo.jpg"));
    }

    [Fact]
    public async Task DownloadAsync_PropagatesHttpFailures()
    {
        var handler = new StubHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.NotFound));
        var downloader = new MediaDownloader(new HttpClient(handler));

        var act = () => downloader.DownloadAsync(
            new Uri("https://cdn.example.test/missing.jpg"),
            CancellationToken.None);

        await act.Should().ThrowAsync<HttpRequestException>();
    }

    private sealed class StubHttpMessageHandler(
        Func<HttpRequestMessage, HttpResponseMessage> responseFactory) : HttpMessageHandler
    {
        public HttpRequestMessage? LastRequest { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            LastRequest = request;
            return Task.FromResult(responseFactory(request));
        }
    }
}
