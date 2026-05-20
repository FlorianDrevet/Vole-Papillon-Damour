using Azure.Storage.Blobs;
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
    
    private async Task<Uri> UploadAsync(string fileName, Stream stream, BlobContainerClient blobContainerClient)
    {
        var blobClient = blobContainerClient.GetBlobClient(fileName);
        await blobClient.UploadAsync(stream, true);
        return blobClient.Uri;
    }

    public Task<string> DeleteFileAsync(string fileName)
    {
        throw new NotImplementedException();
    }
}