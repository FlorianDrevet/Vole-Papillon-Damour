using Vole_Papillon_Damour.Application.Common.Interfaces.Services;

namespace Vole_Papillon_Damour.Infrastructure.Services.Social;

public sealed class MediaDownloader(HttpClient httpClient) : IMediaDownloader
{
    public async Task<Stream> DownloadAsync(
        Uri mediaUri,
        CancellationToken cancellationToken)
    {
        using var response = await httpClient.GetAsync(
            mediaUri,
            HttpCompletionOption.ResponseHeadersRead,
            cancellationToken);
        response.EnsureSuccessStatusCode();

        var content = new MemoryStream();
        await response.Content.CopyToAsync(content, cancellationToken);
        content.Position = 0;
        return content;
    }
}
