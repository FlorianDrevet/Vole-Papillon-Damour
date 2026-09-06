using System.Net.Http.Headers;

namespace Vole_Papillon_Damour.Infrastructure.Services.Bibliographic;

internal static class CoverImageValidator
{
    public static async Task<bool> IsValidAsync(
        HttpClient httpClient,
        Uri coverUri,
        CancellationToken cancellationToken)
    {
        if (!coverUri.IsAbsoluteUri ||
            !string.Equals(coverUri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, coverUri);
            using var response = await httpClient.SendAsync(
                request,
                HttpCompletionOption.ResponseHeadersRead,
                cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                return false;
            }

            return IsImage(response.Content.Headers.ContentType);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return false;
        }
        catch (HttpRequestException)
        {
            return false;
        }
    }

    private static bool IsImage(MediaTypeHeaderValue? contentType)
    {
        return contentType?.MediaType?.StartsWith("image/", StringComparison.OrdinalIgnoreCase) == true;
    }
}
