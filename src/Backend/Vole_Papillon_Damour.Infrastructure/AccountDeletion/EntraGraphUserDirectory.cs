using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.Extensions.Options;
using Vole_Papillon_Damour.Application.AccountDeletion;
using Vole_Papillon_Damour.Application.Common.Interfaces.Services;

namespace Vole_Papillon_Damour.Infrastructure.AccountDeletion;

public sealed class EntraGraphUserDirectory(
    HttpClient httpClient,
    IOptions<EntraGraphOptions> options) : IEntraUserDirectory
{
    private static readonly Uri GraphUsersUri = new("https://graph.microsoft.com/v1.0/users/");
    private readonly EntraGraphOptions _options = options.Value;

    public async Task DeleteAsync(string externalId, CancellationToken cancellationToken)
    {
        EnsureConfigured();

        var accessToken = await GetAccessTokenAsync(cancellationToken);
        using var request = new HttpRequestMessage(
            HttpMethod.Delete,
            new Uri(GraphUsersUri, Uri.EscapeDataString(externalId)));
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        using var response = await httpClient.SendAsync(request, cancellationToken);
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return;
        }

        if (!response.IsSuccessStatusCode)
        {
            throw new AccountDeletionDependencyException($"graph-http-{(int)response.StatusCode}");
        }
    }

    private async Task<string> GetAccessTokenAsync(CancellationToken cancellationToken)
    {
        var tokenEndpoint = new Uri(
            $"https://login.microsoftonline.com/{Uri.EscapeDataString(_options.TenantId)}/oauth2/v2.0/token");
        using var request = new HttpRequestMessage(HttpMethod.Post, tokenEndpoint)
        {
            Content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["client_id"] = _options.ClientId,
                ["client_secret"] = _options.ClientSecret,
                ["scope"] = "https://graph.microsoft.com/.default",
                ["grant_type"] = "client_credentials"
            })
        };

        using var response = await httpClient.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            throw new AccountDeletionDependencyException($"graph-token-http-{(int)response.StatusCode}");
        }

        var token = await response.Content.ReadFromJsonAsync<AccessTokenResponse>(cancellationToken);
        if (string.IsNullOrWhiteSpace(token?.AccessToken))
        {
            throw new AccountDeletionDependencyException("graph-token-invalid");
        }

        return token.AccessToken;
    }

    private void EnsureConfigured()
    {
        if (string.IsNullOrWhiteSpace(_options.TenantId)
            || string.IsNullOrWhiteSpace(_options.ClientId)
            || string.IsNullOrWhiteSpace(_options.ClientSecret))
        {
            throw new AccountDeletionDependencyException("graph-not-configured");
        }
    }

    private sealed record AccessTokenResponse(
        [property: System.Text.Json.Serialization.JsonPropertyName("access_token")] string? AccessToken);
}
