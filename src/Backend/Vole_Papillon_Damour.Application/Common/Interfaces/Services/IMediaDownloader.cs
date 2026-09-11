namespace Vole_Papillon_Damour.Application.Common.Interfaces.Services;

public interface IMediaDownloader
{
    Task<Stream> DownloadAsync(Uri mediaUri, CancellationToken cancellationToken);
}
