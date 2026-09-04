using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Vole_Papillon_Damour.Application.Common.Interfaces.Services;
using Vole_Papillon_Damour.Domain.BookAggregate.ValueObjects;

namespace Vole_Papillon_Damour.Infrastructure.Services.Bibliographic;

public sealed class BookCoverStorage(
    HttpClient httpClient,
    IBlobService blobService,
    IOptions<BookCoverOptions> options,
    ILogger<BookCoverStorage> logger) : IBookCoverStorage
{
    private const string DefaultContentType = "image/jpeg";
    private readonly BookCoverOptions _options = options.Value;

    public async Task<Uri?> TryStoreAsync(
        Isbn13 isbn13,
        Uri sourceUri,
        CancellationToken cancellationToken)
    {
        if (!_options.Enabled ||
            !IsAllowedSource(sourceUri) ||
            _options.MaxBytes <= 0)
        {
            return null;
        }

        using var request = new HttpRequestMessage(HttpMethod.Get, sourceUri);
        using var response = await httpClient.SendAsync(
            request,
            HttpCompletionOption.ResponseHeadersRead,
            cancellationToken);

        if (!response.IsSuccessStatusCode ||
            (response.Content.Headers.ContentLength is long contentLength &&
             contentLength > _options.MaxBytes))
        {
            return null;
        }

        var contentType = response.Content.Headers.ContentType?.MediaType;
        if (!string.IsNullOrWhiteSpace(contentType) &&
            !contentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        await using var sourceStream = await response.Content.ReadAsStreamAsync(cancellationToken);
        await using var coverStream = new MemoryStream();
        var copiedBytes = await CopyUpToLimitAsync(
            sourceStream,
            coverStream,
            _options.MaxBytes,
            cancellationToken);
        if (copiedBytes == 0 || copiedBytes > _options.MaxBytes)
        {
            return null;
        }

        coverStream.Position = 0;
        var fileName = $"{_options.ContainerPrefix.Trim('/')}/{isbn13.Value}.jpg";
        try
        {
            return await blobService.UploadBookCoverAsync(
                fileName,
                coverStream,
                contentType ?? DefaultContentType);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            logger.LogWarning(
                exception,
                "Could not store the cover for ISBN {Isbn13}; metadata enrichment will continue.",
                isbn13.Value);
            return null;
        }
    }

    private bool IsAllowedSource(Uri sourceUri)
    {
        if (!sourceUri.IsAbsoluteUri ||
            sourceUri.Scheme != Uri.UriSchemeHttps ||
            string.IsNullOrWhiteSpace(sourceUri.Host))
        {
            return false;
        }

        return _options.AllowedHosts.Any(host =>
            string.Equals(host, sourceUri.Host, StringComparison.OrdinalIgnoreCase));
    }

    private static async Task<long> CopyUpToLimitAsync(
        Stream source,
        Stream destination,
        int maxBytes,
        CancellationToken cancellationToken)
    {
        var buffer = new byte[81920];
        long copiedBytes = 0;
        int read;
        while ((read = await source.ReadAsync(buffer, cancellationToken)) > 0)
        {
            copiedBytes += read;
            if (copiedBytes > maxBytes)
            {
                return copiedBytes;
            }

            await destination.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
        }

        return copiedBytes;
    }
}
