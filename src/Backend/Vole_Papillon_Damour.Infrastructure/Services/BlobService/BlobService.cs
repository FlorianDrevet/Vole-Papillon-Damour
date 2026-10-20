using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Azure;
using Microsoft.Extensions.Options;
using Vole_Papillon_Damour.Application.Common.Interfaces.Services;

namespace Vole_Papillon_Damour.Infrastructure.Services.BlobService;

public class BlobService
    : IBlobService
{
    private readonly BlobContainerClient _blobContainerClient;
    private readonly BlobContainerClient _blobContainerActualityImagesClient;
    private readonly BlobContainerClient _blobContainerEventImagesClient;
    private readonly BlobContainerClient _blobContaineProductsImagesClient;
    private readonly BlobContainerClient _blobContainerRareBookPhotosClient;
    private readonly BlobContainerClient _blobContainerBookCoversClient;

    public BlobService(
        BlobServiceClient blobServiceClient,
        IOptions<BlobSettings> blobStorageSettings)
    {
        _blobContainerClient = blobServiceClient.GetBlobContainerClient(blobStorageSettings.Value.ContainerName);
        _blobContainerActualityImagesClient = blobServiceClient
            .GetBlobContainerClient(blobStorageSettings.Value.ContainerActualityImagesName);
        _blobContainerEventImagesClient = blobServiceClient
            .GetBlobContainerClient(blobStorageSettings.Value.BlobContainerEventImagesClient);
        _blobContaineProductsImagesClient = blobServiceClient
            .GetBlobContainerClient(blobStorageSettings.Value.BlobContainerProductsImagesClient);
        _blobContainerRareBookPhotosClient = blobServiceClient
            .GetBlobContainerClient(blobStorageSettings.Value.BlobContainerRareBookPhotosClient);
        _blobContainerBookCoversClient = blobServiceClient
            .GetBlobContainerClient(blobStorageSettings.Value.BlobContainerBookCoversClient);
    }
    
    public async Task<Uri> UploadLotoImagesAsync(string fileName, Stream stream)
    {
        return await UploadAsync(fileName, stream, _blobContainerClient);
    }
    
    public async Task<Uri> UploadProductsImagesAsync(string fileName, Stream stream)
    {
        return await UploadAsync(fileName, stream, _blobContaineProductsImagesClient);
    }
    
    public async Task<Uri> UploadActualityImagesAsync(string fileName, Stream stream)
    {
        return await UploadAsync(fileName, stream, _blobContainerActualityImagesClient);
    }
    
    public async Task<Uri> UploadEventImagesAsync(string fileName, Stream stream)
    {
        return await UploadAsync(fileName, stream, _blobContainerEventImagesClient);
    }

    public async Task<Uri> UploadRareBookPhotoAsync(string fileName, Stream stream)
    {
        return await UploadAsync(fileName, stream, _blobContainerRareBookPhotosClient);
    }

    public async Task<Uri> UploadBookCoverAsync(
        string fileName,
        Stream stream,
        string contentType,
        CancellationToken cancellationToken)
    {
        await _blobContainerBookCoversClient.CreateIfNotExistsAsync(
            PublicAccessType.Blob,
            cancellationToken: cancellationToken);

        var blobClient = _blobContainerBookCoversClient.GetBlobClient(fileName);
        await blobClient.UploadAsync(
            stream,
            new BlobUploadOptions
            {
                HttpHeaders = new BlobHttpHeaders { ContentType = contentType }
            },
            cancellationToken);
        return blobClient.Uri;
    }

    private async Task<Uri> UploadAsync(string fileName, Stream stream, BlobContainerClient blobContainerClient)
    {
        var blobClient = blobContainerClient.GetBlobClient(fileName);
        await blobClient.UploadAsync(stream, true);
        return blobClient.Uri;
    }

    public async Task<string> DeleteFileAsync(BlobContainer container, string blobNameOrUri)
    {
        if (string.IsNullOrWhiteSpace(blobNameOrUri))
        {
            return string.Empty;
        }

        var blobContainerClient = GetBlobContainerClient(container);
        var blobName = blobNameOrUri;
        if (Uri.TryCreate(blobNameOrUri, UriKind.Absolute, out var uri))
        {
            var containerUri = blobContainerClient.Uri;
            var containerPath = containerUri.AbsolutePath.TrimEnd('/') + "/";
            if (!string.Equals(uri.Host, containerUri.Host, StringComparison.OrdinalIgnoreCase) ||
                !uri.AbsolutePath.StartsWith(containerPath, StringComparison.Ordinal))
            {
                return string.Empty;
            }

            blobName = Uri.UnescapeDataString(uri.AbsolutePath[containerPath.Length..]);
        }

        if (string.IsNullOrWhiteSpace(blobName))
        {
            return string.Empty;
        }

        var blobClient = blobContainerClient.GetBlobClient(blobName);
        await blobClient.DeleteIfExistsAsync();
        return blobName;
    }

    private BlobContainerClient GetBlobContainerClient(BlobContainer container)
    {
        return container switch
        {
            BlobContainer.LotoImages => _blobContainerClient,
            BlobContainer.ActualityImages => _blobContainerActualityImagesClient,
            BlobContainer.EventImages => _blobContainerEventImagesClient,
            BlobContainer.ProductsImages => _blobContaineProductsImagesClient,
            BlobContainer.RareBookPhotos => _blobContainerRareBookPhotosClient,
            BlobContainer.BookCovers => _blobContainerBookCoversClient,
            _ => throw new ArgumentOutOfRangeException(nameof(container), container, "Unknown blob container.")
        };
    }
}
