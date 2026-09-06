using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Options;
using Vole_Papillon_Damour.Application.AccountAdministration;
using Vole_Papillon_Damour.Application.AccountDeletion;
using Vole_Papillon_Damour.Application.Common.Interfaces.Services;

namespace Vole_Papillon_Damour.Infrastructure.AccountDeletion;

public sealed class EntraGraphUserDirectory(
    HttpClient httpClient,
    IOptions<EntraGraphOptions> options) : IEntraUserDirectory, IEntraAccountDirectory
{
    private const string GraphRoot = "https://graph.microsoft.com/v1.0/";
    private static readonly Uri GraphUsersUri = new($"{GraphRoot}users/");
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

    public async Task<IReadOnlyList<EntraAccount>> ListAsync(CancellationToken cancellationToken)
    {
        EnsureAccountManagementConfigured();
        var accessToken = await GetAccessTokenAsync(cancellationToken, accountManagement: true);
        var servicePrincipal = await GetApiServicePrincipalAsync(accessToken, cancellationToken);
        var users = await GetCollectionAsync<GraphUser>(
            new Uri($"{GraphRoot}users?$select=id,displayName,mail,userPrincipalName,accountEnabled,createdDateTime,identities&$top=999"),
            accessToken,
            cancellationToken);
        var assignments = await GetCollectionAsync<GraphAppRoleAssignment>(
            new Uri($"{GraphRoot}servicePrincipals/{Uri.EscapeDataString(servicePrincipal.Id)}/appRoleAssignedTo?$select=principalId,appRoleId&$top=999"),
            accessToken,
            cancellationToken);
        var roleNames = GetSupportedRoleNames(servicePrincipal);
        var rolesByAccount = assignments
            .Where(assignment => roleNames.ContainsKey(assignment.AppRoleId))
            .GroupBy(assignment => assignment.PrincipalId, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                group => group.Key,
                group => (IReadOnlyCollection<string>)group
                    .Select(assignment => roleNames[assignment.AppRoleId])
                    .Distinct(StringComparer.Ordinal)
                    .OrderBy(role => role, StringComparer.Ordinal)
                    .ToArray(),
                StringComparer.OrdinalIgnoreCase);

        return users
            .Select(user => ToAccount(
                user,
                rolesByAccount.TryGetValue(user.Id, out var roles) ? roles : []))
            .ToArray();
    }

    public async Task<EntraAccount> CreateAsync(
        string email,
        string displayName,
        string temporaryPassword,
        IReadOnlyCollection<string> roles,
        CancellationToken cancellationToken)
    {
        EnsureAccountManagementConfigured();
        var accessToken = await GetAccessTokenAsync(cancellationToken, accountManagement: true);
        var user = await SendJsonAsync<GraphUser>(
            HttpMethod.Post,
            new Uri($"{GraphRoot}users"),
            accessToken,
            new GraphCreateUserRequest(
                true,
                displayName,
                email,
                new GraphPasswordProfile(temporaryPassword, true),
                "DisablePasswordExpiration",
                [new GraphIdentity("emailAddress", _options.TenantDomain, email)]),
            cancellationToken);

        return await ApplyRolesAsync(user, roles, accessToken, cancellationToken);
    }

    public async Task<EntraAccount> SetRolesAsync(
        string externalId,
        IReadOnlyCollection<string> roles,
        CancellationToken cancellationToken)
    {
        EnsureAccountManagementConfigured();
        var accessToken = await GetAccessTokenAsync(cancellationToken, accountManagement: true);
        var user = await SendJsonAsync<GraphUser>(
            HttpMethod.Get,
            new Uri($"{GraphRoot}users/{Uri.EscapeDataString(externalId)}?$select=id,displayName,mail,userPrincipalName,accountEnabled,createdDateTime,identities"),
            accessToken,
            payload: null,
            cancellationToken);

        return await ApplyRolesAsync(user, roles, accessToken, cancellationToken);
    }

    private async Task<EntraAccount> ApplyRolesAsync(
        GraphUser user,
        IReadOnlyCollection<string> roles,
        string accessToken,
        CancellationToken cancellationToken)
    {
        var servicePrincipal = await GetApiServicePrincipalAsync(accessToken, cancellationToken);
        var roleNames = GetSupportedRoleNames(servicePrincipal);
        var requestedRoleIds = roles
            .Select(role => roleNames.FirstOrDefault(pair =>
                string.Equals(pair.Value, role, StringComparison.OrdinalIgnoreCase)).Key)
            .Where(roleId => roleId != Guid.Empty)
            .ToHashSet();

        if (requestedRoleIds.Count != roles.Count)
        {
            throw new EntraAccountDirectoryException("graph-api-role-not-found");
        }

        var assignments = await GetCollectionAsync<GraphAppRoleAssignment>(
            new Uri($"{GraphRoot}users/{Uri.EscapeDataString(user.Id)}/appRoleAssignments?$select=id,appRoleId,resourceId&$top=999"),
            accessToken,
            cancellationToken);
        var apiAssignments = assignments
            .Where(assignment => string.Equals(assignment.ResourceId, servicePrincipal.Id, StringComparison.OrdinalIgnoreCase))
            .ToArray();

        foreach (var assignment in apiAssignments.Where(assignment => !requestedRoleIds.Contains(assignment.AppRoleId)))
        {
            await SendAsync(
                HttpMethod.Delete,
                new Uri($"{GraphRoot}users/{Uri.EscapeDataString(user.Id)}/appRoleAssignments/{Uri.EscapeDataString(assignment.Id)}"),
                accessToken,
                payload: null,
                cancellationToken);
        }

        var currentRoleIds = apiAssignments.Select(assignment => assignment.AppRoleId).ToHashSet();
        foreach (var roleId in requestedRoleIds.Where(roleId => !currentRoleIds.Contains(roleId)))
        {
            await SendAsync(
                HttpMethod.Post,
                new Uri($"{GraphRoot}users/{Uri.EscapeDataString(user.Id)}/appRoleAssignments"),
                accessToken,
                new GraphAppRoleAssignmentRequest(user.Id, servicePrincipal.Id, roleId),
                cancellationToken);
        }

        var assignedRoleNames = roleNames
            .Where(pair => requestedRoleIds.Contains(pair.Key))
            .Select(pair => pair.Value)
            .OrderBy(role => role, StringComparer.Ordinal)
            .ToArray();
        return ToAccount(user, assignedRoleNames);
    }

    private async Task<GraphServicePrincipal> GetApiServicePrincipalAsync(
        string accessToken,
        CancellationToken cancellationToken)
    {
        var filter = Uri.EscapeDataString($"appId eq '{_options.ApiClientId}'");
        var result = await SendJsonAsync<GraphCollection<GraphServicePrincipal>>(
            HttpMethod.Get,
            new Uri($"{GraphRoot}servicePrincipals?$filter={filter}&$select=id,appRoles&$top=1"),
            accessToken,
            payload: null,
            cancellationToken);
        var servicePrincipal = result.Value.FirstOrDefault();
        return servicePrincipal ?? throw new EntraAccountDirectoryException("graph-api-service-principal-not-found");
    }

    private async Task<IReadOnlyList<T>> GetCollectionAsync<T>(
        Uri requestUri,
        string accessToken,
        CancellationToken cancellationToken)
    {
        var values = new List<T>();
        Uri? nextUri = requestUri;
        while (nextUri is not null)
        {
            var page = await SendJsonAsync<GraphCollection<T>>(
                HttpMethod.Get,
                nextUri,
                accessToken,
                payload: null,
                cancellationToken);
            values.AddRange(page.Value);
            nextUri = string.IsNullOrWhiteSpace(page.NextLink) ? null : new Uri(page.NextLink);
        }

        return values;
    }

    private async Task<T> SendJsonAsync<T>(
        HttpMethod method,
        Uri requestUri,
        string accessToken,
        object? payload,
        CancellationToken cancellationToken)
    {
        using var response = await SendCoreAsync(method, requestUri, accessToken, payload, cancellationToken);
        var value = await response.Content.ReadFromJsonAsync<T>(cancellationToken);
        return value ?? throw new EntraAccountDirectoryException("graph-response-invalid");
    }

    private async Task SendAsync(
        HttpMethod method,
        Uri requestUri,
        string accessToken,
        object? payload,
        CancellationToken cancellationToken)
    {
        using var response = await SendCoreAsync(method, requestUri, accessToken, payload, cancellationToken);
    }

    private async Task<HttpResponseMessage> SendCoreAsync(
        HttpMethod method,
        Uri requestUri,
        string accessToken,
        object? payload,
        CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(method, requestUri);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        if (payload is not null)
        {
            request.Content = JsonContent.Create(payload);
        }

        var response = await httpClient.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            var statusCode = (int)response.StatusCode;
            response.Dispose();
            throw new EntraAccountDirectoryException($"graph-http-{statusCode}", statusCode);
        }

        return response;
    }

    private async Task<string> GetAccessTokenAsync(
        CancellationToken cancellationToken,
        bool accountManagement = false)
    {
        if (accountManagement)
        {
            EnsureAccountManagementConfigured();
        }
        else
        {
            EnsureConfigured();
        }

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
            if (accountManagement)
            {
                throw new EntraAccountDirectoryException(
                    $"graph-token-http-{(int)response.StatusCode}",
                    (int)response.StatusCode);
            }

            throw new AccountDeletionDependencyException($"graph-token-http-{(int)response.StatusCode}");
        }

        var token = await response.Content.ReadFromJsonAsync<AccessTokenResponse>(cancellationToken);
        if (string.IsNullOrWhiteSpace(token?.AccessToken))
        {
            if (accountManagement)
            {
                throw new EntraAccountDirectoryException("graph-token-invalid");
            }

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

    private void EnsureAccountManagementConfigured()
    {
        if (string.IsNullOrWhiteSpace(_options.TenantId)
            || string.IsNullOrWhiteSpace(_options.ClientId)
            || string.IsNullOrWhiteSpace(_options.ClientSecret)
            || string.IsNullOrWhiteSpace(_options.TenantDomain)
            || string.IsNullOrWhiteSpace(_options.ApiClientId))
        {
            throw new EntraAccountDirectoryException("graph-account-management-not-configured");
        }
    }

    private static Dictionary<Guid, string> GetSupportedRoleNames(GraphServicePrincipal servicePrincipal)
    {
        return servicePrincipal.AppRoles
            .Where(role => role.Value is not null
                && role.IsEnabled
                && Guid.TryParse(role.Id, out _)
                && AccountRoles.IsValid([role.Value]))
            .ToDictionary(role => Guid.Parse(role.Id), role => AccountRoles.Normalize([role.Value!])[0]);
    }

    private static EntraAccount ToAccount(GraphUser user, IReadOnlyCollection<string> roles)
    {
        var email = user.Mail
            ?? user.Identities?.FirstOrDefault(identity =>
                string.Equals(identity.SignInType, "emailAddress", StringComparison.OrdinalIgnoreCase))?.IssuerAssignedId
            ?? user.UserPrincipalName;
        return new EntraAccount(user.Id, email, user.DisplayName, user.AccountEnabled, user.CreatedDateTime, roles);
    }

    private sealed record AccessTokenResponse(
        [property: JsonPropertyName("access_token")] string? AccessToken);

    private sealed record GraphCollection<T>(
        [property: JsonPropertyName("value")] IReadOnlyList<T> Value,
        [property: JsonPropertyName("@odata.nextLink")] string? NextLink = null);

    private sealed record GraphUser(
        [property: JsonPropertyName("id")] string Id,
        [property: JsonPropertyName("displayName")] string? DisplayName,
        [property: JsonPropertyName("mail")] string? Mail,
        [property: JsonPropertyName("userPrincipalName")] string? UserPrincipalName,
        [property: JsonPropertyName("accountEnabled")] bool AccountEnabled,
        [property: JsonPropertyName("createdDateTime")] DateTime? CreatedDateTime,
        [property: JsonPropertyName("identities")] IReadOnlyList<GraphIdentity>? Identities);

    private sealed record GraphIdentity(
        [property: JsonPropertyName("signInType")] string SignInType,
        [property: JsonPropertyName("issuer")] string Issuer,
        [property: JsonPropertyName("issuerAssignedId")] string IssuerAssignedId);

    private sealed record GraphServicePrincipal(
        [property: JsonPropertyName("id")] string Id,
        [property: JsonPropertyName("appRoles")] IReadOnlyList<GraphAppRole> AppRoles);

    private sealed record GraphAppRole(
        [property: JsonPropertyName("id")] string Id,
        [property: JsonPropertyName("value")] string? Value,
        [property: JsonPropertyName("isEnabled")] bool IsEnabled);

    private sealed record GraphAppRoleAssignment(
        [property: JsonPropertyName("id")] string Id,
        [property: JsonPropertyName("principalId")] string PrincipalId,
        [property: JsonPropertyName("appRoleId")] Guid AppRoleId,
        [property: JsonPropertyName("resourceId")] string? ResourceId = null);

    private sealed record GraphCreateUserRequest(
        [property: JsonPropertyName("accountEnabled")] bool AccountEnabled,
        [property: JsonPropertyName("displayName")] string DisplayName,
        [property: JsonPropertyName("mail")] string Mail,
        [property: JsonPropertyName("passwordProfile")] GraphPasswordProfile PasswordProfile,
        [property: JsonPropertyName("passwordPolicies")] string PasswordPolicies,
        [property: JsonPropertyName("identities")] IReadOnlyList<GraphIdentity> Identities);

    private sealed record GraphPasswordProfile(
        [property: JsonPropertyName("password")] string Password,
        [property: JsonPropertyName("forceChangePasswordNextSignIn")] bool ForceChangePasswordNextSignIn);

    private sealed record GraphAppRoleAssignmentRequest(
        [property: JsonPropertyName("principalId")] string PrincipalId,
        [property: JsonPropertyName("resourceId")] string ResourceId,
        [property: JsonPropertyName("appRoleId")] Guid AppRoleId);
}
