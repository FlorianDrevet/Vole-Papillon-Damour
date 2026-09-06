using Microsoft.AspNetCore.Http;

namespace Vole_Papillon_Damour.Application.Common.Interfaces.Services;

public interface IBlobService
{
    public Task<Uri> UploadLotoImagesAsync(string fileName, Stream stream);
    public Task<Uri> UploadProductsImagesAsync(string fileName, Stream stream);
    public Task<Uri> UploadActualityImagesAsync(string fileName, Stream stream);
    public Task<Uri> UploadEventImagesAsync(string fileName, Stream stream);
    public Task<string> DeleteFileAsync(string fileName);
}
