using System.Net;
using Azure.Core.Pipeline;
using Azure.Storage.Blobs;
using FluentAssertions;
using Microsoft.Extensions.Options;
using Vole_Papillon_Damour.Application.Common.Interfaces.Services;
using Vole_Papillon_Damour.Infrastructure.Services.BlobService;

namespace Vole_Papillon_Damour.Infrastructure.tests.Services;

public sealed class BlobServiceTests
{
    [Fact]
    public async Task DeleteFileAsync_NonActualityContainer_DeletesBlobInSelectedContainer()
    {
        var transport = new RecordingHttpMessageHandler();
        var blobClientOptions = new BlobClientOptions
        {
            Transport = new HttpClientTransport(transport)
        };
        var blobServiceClient = new BlobServiceClient(
            new Uri("https://storage.example.test"),
            blobClientOptions);
        var service = new BlobService(
            blobServiceClient,
            Options.Create(new BlobSettings
            {
                ContainerName = "loto-images",
                ContainerActualityImagesName = "actuality-images",
                BlobContainerEventImagesClient = "event-images",
                BlobContainerProductsImagesClient = "product-images",
                BlobContainerRareBookPhotosClient = "livres-rares"
            }));

        var deletedBlobName = await service.DeleteFileAsync(
            BlobContainer.RareBookPhotos,
            "https://storage.example.test/livres-rares/rare/cover%20photo.jpg");

        deletedBlobName.Should().Be("rare/cover photo.jpg");
        transport.Requests.Should().ContainSingle();
        transport.Requests[0].Method.Should().Be(HttpMethod.Delete);
        transport.Requests[0].RequestUri!.AbsolutePath
            .Should().Be("/livres-rares/rare/cover%20photo.jpg");
    }

    private sealed class RecordingHttpMessageHandler : HttpMessageHandler
    {
        public List<HttpRequestMessage> Requests { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            Requests.Add(request);
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.Accepted)
            {
                RequestMessage = request
            });
        }
    }
}
