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
    public async Task UploadRareBookPhotoAsync_UsesTheRareBookContainer()
    {
        var transport = new RecordingHttpMessageHandler();
        var blobServiceClient = new BlobServiceClient(
            new Uri("https://storage.example.test"),
            new BlobClientOptions
            {
                Transport = new HttpClientTransport(transport)
            });
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

        await service.UploadRareBookPhotoAsync(
            "rare-book/photo.webp",
            new MemoryStream([1, 2, 3]));

        transport.Requests.Should().ContainSingle();
        transport.Requests[0].Method.Should().Be(HttpMethod.Put);
        transport.Requests[0].RequestUri!.AbsolutePath
            .Should().Be("/livres-rares/rare-book/photo.webp");
    }

    [Fact]
    public async Task UploadBookCoverAsync_UsesTheBookCoverContainerAndSetsImageContentType()
    {
        var transport = new RecordingHttpMessageHandler();
        var blobServiceClient = new BlobServiceClient(
            new Uri("https://storage.example.test"),
            new BlobClientOptions
            {
                Transport = new HttpClientTransport(transport)
            });
        var service = new BlobService(
            blobServiceClient,
            Options.Create(new BlobSettings
            {
                ContainerName = "loto-images",
                ContainerActualityImagesName = "actuality-images",
                BlobContainerEventImagesClient = "event-images",
                BlobContainerProductsImagesClient = "product-images",
                BlobContainerRareBookPhotosClient = "livres-rares",
                BlobContainerBookCoversClient = "book-covers"
            }));

        var uri = await service.UploadBookCoverAsync(
            "9782070363735/cover.webp",
            new MemoryStream([1, 2, 3]),
            "image/webp",
            CancellationToken.None);

        uri.AbsolutePath.Should().Be("/book-covers/9782070363735/cover.webp");
        transport.Requests.Should().HaveCount(2);
        transport.Requests[0].Method.Should().Be(HttpMethod.Put);
        transport.Requests[0].RequestUri!.AbsolutePath.Should().Be("/book-covers");
        transport.Requests[0].RequestUri!.Query.Should().Contain("restype=container");
        transport.Requests[0].Headers.GetValues("x-ms-blob-public-access")
            .Should().ContainSingle().Which.Should().Be("blob");
        transport.Requests[1].Method.Should().Be(HttpMethod.Put);
        transport.Requests[1].Headers.GetValues("x-ms-blob-content-type")
            .Should().ContainSingle().Which.Should().Be("image/webp");
    }

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
            return Task.FromResult(new HttpResponseMessage(
                request.Method == HttpMethod.Put
                    ? HttpStatusCode.Created
                    : HttpStatusCode.Accepted)
            {
                RequestMessage = request
            });
        }
    }
}
