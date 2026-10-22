using System.Net;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using Microsoft.Extensions.Options;
using Vole_Papillon_Damour.Application.AccountAdministration;
using Vole_Papillon_Damour.Infrastructure.AccountDeletion;

namespace Vole_Papillon_Damour.Infrastructure.tests.AccountAdministration;

public sealed class EntraGraphUserDirectoryAccountTests
{
    [Fact]
    public async Task CreateAsync_CreatesLocalIdentityAndAssignsRequestedRoles()
    {
        var requests = new List<HttpRequestMessage>();
        var createBodies = new List<string>();
        using var client = new HttpClient(new RecordingHandler(request =>
        {
            requests.Add(request);
            var path = request.RequestUri!.AbsolutePath;
            if (request.Method == HttpMethod.Post && path.EndsWith("/token", StringComparison.Ordinal))
            {
                return JsonResponse("{\"access_token\":\"access-token\"}");
            }

            if (request.Method == HttpMethod.Post && path == "/v1.0/users")
            {
                createBodies.Add(request.Content!.ReadAsStringAsync().GetAwaiter().GetResult());
                return JsonResponse("""
                    {"id":"account-1","displayName":"Marie Tri","mail":null,"userPrincipalName":"marie@example.test","accountEnabled":true,"createdDateTime":"2026-09-06T12:00:00Z","identities":[{"signInType":"emailAddress","issuer":"volepapillondamour.onmicrosoft.com","issuerAssignedId":"marie@example.test"}]}
                    """);
            }

            if (request.Method == HttpMethod.Get && path == "/v1.0/servicePrincipals")
            {
                return JsonResponse("""
                    {"value":[{"id":"api-service-principal","appRoles":[{"id":"6b1f0a54-2c3d-4e5f-9a8b-7c6d5e4f3a21","value":"Tri","isEnabled":true},{"id":"c7a5e3d1-8f2b-4c6a-9d0e-3b4c5d6e7f80","value":"Administration","isEnabled":true}]}]}
                    """);
            }

            if (request.Method == HttpMethod.Get && path == "/v1.0/users/account-1/appRoleAssignments")
            {
                return JsonResponse("{\"value\":[]}");
            }

            if (request.Method == HttpMethod.Post && path == "/v1.0/users/account-1/appRoleAssignments")
            {
                return JsonResponse("{\"id\":\"assignment-1\"}");
            }

            throw new InvalidOperationException($"Unexpected Graph request: {request.Method} {request.RequestUri}");
        }));
        var options = Options.Create(new EntraGraphOptions
        {
            TenantId = "tenant-id",
            TenantDomain = "volepapillondamour.onmicrosoft.com",
            ApiClientId = "api-client-id",
            ClientId = "client-id",
            ClientSecret = "client-secret"
        });
        var directory = new EntraGraphUserDirectory(client, options);

        var account = await directory.CreateAsync(
            "marie@example.test",
            "Marie",
            "Tri",
            "Temporaire1!",
            ["Tri"],
            CancellationToken.None);

        account.ExternalId.Should().Be("account-1");
        account.Roles.Should().ContainSingle().Which.Should().Be("Tri");
        requests.Should().Contain(request =>
            request.Method == HttpMethod.Post &&
            request.RequestUri!.AbsolutePath == "/v1.0/users/account-1/appRoleAssignments");
        var createBody = createBodies.Single();
        createBody.Should().Contain("\"displayName\":\"Marie Tri\"");
        createBody.Should().Contain("\"givenName\":\"Marie\"");
        createBody.Should().Contain("\"surname\":\"Tri\"");
        createBody.Should().Contain("\"issuer\":\"volepapillondamour.onmicrosoft.com\"");
        createBody.Should().Contain("\"issuerAssignedId\":\"marie@example.test\"");
        createBody.Should().Contain("\"forceChangePasswordNextSignIn\":true");
        createBody.Should().Contain("\"passwordPolicies\":\"DisablePasswordExpiration\"");
    }

    [Fact]
    public async Task CreateAsync_WhenGraphRejectsPasswordPolicy_IdentifiesTheTemporaryPasswordFailure()
    {
        using var client = new HttpClient(new RecordingHandler(request =>
        {
            var path = request.RequestUri!.AbsolutePath;
            if (request.Method == HttpMethod.Post && path.EndsWith("/token", StringComparison.Ordinal))
            {
                return JsonResponse("{\"access_token\":\"access-token\"}");
            }

            if (request.Method == HttpMethod.Post && path == "/v1.0/users")
            {
                return JsonResponse(
                    "{\"error\":{\"code\":\"Request_BadRequest\",\"message\":\"The password does not meet the length, complexity, history, or age requirements of the domain.\"}}",
                    HttpStatusCode.BadRequest);
            }

            throw new InvalidOperationException($"Unexpected Graph request: {request.Method} {request.RequestUri}");
        }));
        var options = Options.Create(new EntraGraphOptions
        {
            TenantId = "tenant-id",
            TenantDomain = "volepapillondamour.onmicrosoft.com",
            ApiClientId = "api-client-id",
            ClientId = "client-id",
            ClientSecret = "client-secret"
        });
        var directory = new EntraGraphUserDirectory(client, options);

        var exception = await FluentActions.Invoking(() => directory.CreateAsync(
                "marie@example.test",
                "Marie",
                "Tri",
                "too-weak",
                ["Tri"],
                CancellationToken.None))
            .Should()
            .ThrowAsync<EntraAccountDirectoryException>();

        exception.Which.FailureCode.Should().Be(EntraAccountDirectoryException.TemporaryPasswordRejected);
        exception.Which.StatusCode.Should().Be((int)HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task ListAsync_TreatsOmittedGraphCollectionsAsEmptyAndKeepsDisplayName()
    {
        using var client = new HttpClient(new RecordingHandler(request =>
        {
            var path = request.RequestUri!.AbsolutePath;
            if (request.Method == HttpMethod.Post && path.EndsWith("/token", StringComparison.Ordinal))
            {
                return JsonResponse("{\"access_token\":\"access-token\"}");
            }

            if (request.Method == HttpMethod.Get && path == "/v1.0/servicePrincipals")
            {
                return JsonResponse("{\"value\":[{\"id\":\"api-service-principal\"}]}");
            }

            if (request.Method == HttpMethod.Get && path == "/v1.0/users")
            {
                return JsonResponse("""
                    {"value":[{"id":"account-1","displayName":"Ada Lovelace","mail":null,"userPrincipalName":"ada@example.test","accountEnabled":true,"createdDateTime":"2026-09-06T12:00:00Z","identities":[]}]}
                    """);
            }

            if (request.Method == HttpMethod.Get && path.EndsWith("/appRoleAssignedTo", StringComparison.Ordinal))
            {
                return JsonResponse("{}");
            }

            throw new InvalidOperationException($"Unexpected Graph request: {request.Method} {request.RequestUri}");
        }));
        var options = Options.Create(new EntraGraphOptions
        {
            TenantId = "tenant-id",
            TenantDomain = "volepapillondamour.onmicrosoft.com",
            ApiClientId = "api-client-id",
            ClientId = "client-id",
            ClientSecret = "client-secret"
        });
        var directory = new EntraGraphUserDirectory(client, options);

        var accounts = await directory.ListAsync(CancellationToken.None);

        accounts.Should().ContainSingle();
        accounts[0].DisplayName.Should().Be("Ada Lovelace");
        accounts[0].Email.Should().Be("ada@example.test");
        accounts[0].Roles.Should().BeEmpty();
    }

    [Fact]
    public async Task ListAsync_MarksOnlyTenantEmailIdentitiesAsLocalEmailPasswordAccounts()
    {
        using var client = new HttpClient(new RecordingHandler(request =>
        {
            var path = request.RequestUri!.AbsolutePath;
            if (request.Method == HttpMethod.Post && path.EndsWith("/token", StringComparison.Ordinal))
            {
                return JsonResponse("{\"access_token\":\"access-token\"}");
            }

            if (request.Method == HttpMethod.Get && path == "/v1.0/servicePrincipals")
            {
                return JsonResponse(ApiServicePrincipal());
            }

            if (request.Method == HttpMethod.Get && path == "/v1.0/users")
            {
                return JsonResponse("""
                    {"value":[
                      {"id":"local","displayName":"Local","mail":"local@example.test","userPrincipalName":"local@example.test","accountEnabled":true,"createdDateTime":"2026-10-22T00:00:00Z","identities":[{"signInType":"emailAddress","issuer":"VOLEPAPILLONDAMOUR.ONMICROSOFT.COM","issuerAssignedId":"local@example.test"}]},
                      {"id":"external","displayName":"External","mail":"external@example.test","userPrincipalName":"external@example.test","accountEnabled":true,"createdDateTime":"2026-10-22T00:00:00Z","identities":[{"signInType":"federated","issuer":"external.example","issuerAssignedId":"external@example.test"}]}
                    ]}
                    """);
            }

            if (request.Method == HttpMethod.Get && path.EndsWith("/appRoleAssignedTo", StringComparison.Ordinal))
            {
                return JsonResponse("""
                    {"value":[
                      {"principalId":"local","appRoleId":"6b1f0a54-2c3d-4e5f-9a8b-7c6d5e4f3a21"},
                      {"principalId":"external","appRoleId":"6b1f0a54-2c3d-4e5f-9a8b-7c6d5e4f3a21"}
                    ]}
                    """);
            }

            throw new InvalidOperationException($"Unexpected Graph request: {request.Method} {request.RequestUri}");
        }));
        var directory = new EntraGraphUserDirectory(client, CreateOptions());

        var accounts = await directory.ListAsync(CancellationToken.None);

        accounts.Should().HaveCount(2);
        accounts.Single(account => account.ExternalId == "local").IsLocalEmailPassword.Should().BeTrue();
        accounts.Single(account => account.ExternalId == "external").IsLocalEmailPassword.Should().BeFalse();
    }

    [Fact]
    public async Task SetAccountEnabledAsync_UpdatesGraphUserAndReturnsTheAccountWithItsRoles()
    {
        var requests = new List<HttpRequestMessage>();
        var statusBodies = new List<string>();
        using var client = new HttpClient(new RecordingHandler(request =>
        {
            requests.Add(request);
            var path = request.RequestUri!.AbsolutePath;
            if (request.Method == HttpMethod.Post && path.EndsWith("/token", StringComparison.Ordinal))
            {
                return JsonResponse("{\"access_token\":\"access-token\"}");
            }

            if (request.Method == HttpMethod.Get && path == "/v1.0/users/account-1")
            {
                return JsonResponse("""
                    {"id":"account-1","displayName":"Ada Lovelace","mail":null,"userPrincipalName":"ada@example.test","accountEnabled":true,"createdDateTime":"2026-09-06T12:00:00Z","identities":[]}
                    """);
            }

            if (request.Method == HttpMethod.Patch && path == "/v1.0/users/account-1")
            {
                statusBodies.Add(request.Content!.ReadAsStringAsync().GetAwaiter().GetResult());
                return JsonResponse("{}");
            }

            if (request.Method == HttpMethod.Get && path == "/v1.0/servicePrincipals")
            {
                return JsonResponse("""
                    {"value":[{"id":"api-service-principal","appRoles":[{"id":"6b1f0a54-2c3d-4e5f-9a8b-7c6d5e4f3a21","value":"Tri","isEnabled":true}]}]}
                    """);
            }

            if (request.Method == HttpMethod.Get && path == "/v1.0/users/account-1/appRoleAssignments")
            {
                return JsonResponse("{\"value\":[{\"id\":\"assignment-1\",\"principalId\":\"account-1\",\"appRoleId\":\"6b1f0a54-2c3d-4e5f-9a8b-7c6d5e4f3a21\",\"resourceId\":\"api-service-principal\"}]}");
            }

            throw new InvalidOperationException($"Unexpected Graph request: {request.Method} {request.RequestUri}");
        }));
        var options = Options.Create(new EntraGraphOptions
        {
            TenantId = "tenant-id",
            TenantDomain = "volepapillondamour.onmicrosoft.com",
            ApiClientId = "api-client-id",
            ClientId = "client-id",
            ClientSecret = "client-secret"
        });
        var directory = new EntraGraphUserDirectory(client, options);

        var account = await directory.SetAccountEnabledAsync(
            "account-1",
            false,
            CancellationToken.None);

        account.AccountEnabled.Should().BeFalse();
        account.Roles.Should().ContainSingle().Which.Should().Be("Tri");
        statusBodies.Should().ContainSingle().Which.Should().Contain("\"accountEnabled\":false");
        requests.Should().Contain(request => request.Method == HttpMethod.Patch && request.RequestUri!.AbsolutePath == "/v1.0/users/account-1");
    }

    [Fact]
    public async Task ResetPasswordAsync_UpdatesOnlyAnEligibleLocalVolunteerAndForcesPasswordChange()
    {
        var patchBodies = new List<string>();
        using var client = new HttpClient(new RecordingHandler(request =>
        {
            var path = request.RequestUri!.AbsolutePath;
            if (request.Method == HttpMethod.Post && path.EndsWith("/token", StringComparison.Ordinal))
            {
                return JsonResponse("{\"access_token\":\"access-token\"}");
            }

            if (request.Method == HttpMethod.Get && path == "/v1.0/users/account-1")
            {
                return JsonResponse(LocalVolunteerUser("VOLEPAPILLONDAMOUR.ONMICROSOFT.COM"));
            }

            if (request.Method == HttpMethod.Get && path == "/v1.0/servicePrincipals")
            {
                return JsonResponse(ApiServicePrincipal());
            }

            if (request.Method == HttpMethod.Get && path == "/v1.0/users/account-1/appRoleAssignments")
            {
                return JsonResponse("""
                    {"value":[{"id":"assignment-1","appRoleId":"6b1f0a54-2c3d-4e5f-9a8b-7c6d5e4f3a21","resourceId":"api-service-principal"}]}
                    """);
            }

            if (request.Method == HttpMethod.Patch && path == "/v1.0/users/account-1")
            {
                patchBodies.Add(request.Content!.ReadAsStringAsync().GetAwaiter().GetResult());
                return JsonResponse("{}");
            }

            throw new InvalidOperationException($"Unexpected Graph request: {request.Method} {request.RequestUri}");
        }));
        var directory = new EntraGraphUserDirectory(client, CreateOptions());

        await directory.ResetPasswordAsync("account-1", "SecureTemporary!42", CancellationToken.None);

        patchBodies.Should().ContainSingle();
        using var body = JsonDocument.Parse(patchBodies.Single());
        body.RootElement.GetProperty("passwordProfile").GetProperty("password").GetString()
            .Should().Be("SecureTemporary!42");
        body.RootElement.GetProperty("passwordProfile").GetProperty("forceChangePasswordNextSignIn")
            .GetBoolean().Should().BeTrue();
    }

    [Fact]
    public async Task ResetPasswordAsync_WhenIdentityIsNotLocal_ReturnsTypedConflictWithoutPatch()
    {
        var patchRequested = false;
        using var client = new HttpClient(new RecordingHandler(request =>
        {
            var path = request.RequestUri!.AbsolutePath;
            if (request.Method == HttpMethod.Post && path.EndsWith("/token", StringComparison.Ordinal))
            {
                return JsonResponse("{\"access_token\":\"access-token\"}");
            }

            if (request.Method == HttpMethod.Get && path == "/v1.0/users/account-1")
            {
                return JsonResponse(LocalVolunteerUser("another-tenant.onmicrosoft.com"));
            }

            if (request.Method == HttpMethod.Patch)
            {
                patchRequested = true;
                return JsonResponse("{}");
            }

            throw new InvalidOperationException($"Unexpected Graph request: {request.Method} {request.RequestUri}");
        }));
        var directory = new EntraGraphUserDirectory(client, CreateOptions());

        var exception = await FluentActions.Invoking(() => directory.ResetPasswordAsync(
                "account-1",
                "SecureTemporary!42",
                CancellationToken.None))
            .Should()
            .ThrowAsync<EntraAccountDirectoryException>();

        exception.Which.FailureCode.Should().Be(EntraAccountDirectoryException.PasswordResetNotAllowed);
        exception.Which.StatusCode.Should().Be((int)HttpStatusCode.Conflict);
        patchRequested.Should().BeFalse();
    }

    [Fact]
    public async Task ResetPasswordAsync_WhenAccountHasNoVolunteerRole_ReturnsTypedConflictWithoutPatch()
    {
        var patchRequested = false;
        using var client = new HttpClient(new RecordingHandler(request =>
        {
            var path = request.RequestUri!.AbsolutePath;
            if (request.Method == HttpMethod.Post && path.EndsWith("/token", StringComparison.Ordinal))
            {
                return JsonResponse("{\"access_token\":\"access-token\"}");
            }

            if (request.Method == HttpMethod.Get && path == "/v1.0/users/account-1")
            {
                return JsonResponse(LocalVolunteerUser("volepapillondamour.onmicrosoft.com"));
            }

            if (request.Method == HttpMethod.Get && path == "/v1.0/servicePrincipals")
            {
                return JsonResponse(ApiServicePrincipal());
            }

            if (request.Method == HttpMethod.Get && path == "/v1.0/users/account-1/appRoleAssignments")
            {
                return JsonResponse("""
                    {"value":[{"id":"assignment-1","appRoleId":"c7a5e3d1-8f2b-4c6a-9d0e-3b4c5d6e7f80","resourceId":"api-service-principal"}]}
                    """);
            }

            if (request.Method == HttpMethod.Patch)
            {
                patchRequested = true;
                return JsonResponse("{}");
            }

            throw new InvalidOperationException($"Unexpected Graph request: {request.Method} {request.RequestUri}");
        }));
        var directory = new EntraGraphUserDirectory(client, CreateOptions());

        var exception = await FluentActions.Invoking(() => directory.ResetPasswordAsync(
                "account-1",
                "SecureTemporary!42",
                CancellationToken.None))
            .Should()
            .ThrowAsync<EntraAccountDirectoryException>();

        exception.Which.FailureCode.Should().Be(EntraAccountDirectoryException.PasswordResetNotAllowed);
        exception.Which.StatusCode.Should().Be((int)HttpStatusCode.Conflict);
        patchRequested.Should().BeFalse();
    }

    private static HttpResponseMessage JsonResponse(string json, HttpStatusCode statusCode = HttpStatusCode.OK)
    {
        return new HttpResponseMessage(statusCode)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        };
    }

    private static Microsoft.Extensions.Options.IOptions<EntraGraphOptions> CreateOptions() =>
        Options.Create(new EntraGraphOptions
        {
            TenantId = "tenant-id",
            TenantDomain = "volepapillondamour.onmicrosoft.com",
            ApiClientId = "api-client-id",
            ClientId = "client-id",
            ClientSecret = "client-secret"
        });

    private static string LocalVolunteerUser(string issuer) => $$"""
        {"id":"account-1","displayName":"Ada Lovelace","mail":"ada@example.test","userPrincipalName":"ada@example.test","accountEnabled":true,"createdDateTime":"2026-09-06T12:00:00Z","identities":[{"signInType":"emailAddress","issuer":"{{issuer}}","issuerAssignedId":"ada@example.test"}]}
        """;

    private static string ApiServicePrincipal() => """
        {"value":[{"id":"api-service-principal","appRoles":[{"id":"6b1f0a54-2c3d-4e5f-9a8b-7c6d5e4f3a21","value":"Tri","isEnabled":true},{"id":"c7a5e3d1-8f2b-4c6a-9d0e-3b4c5d6e7f80","value":"Administration","isEnabled":true}]}]}
        """;

    private sealed class RecordingHandler(Func<HttpRequestMessage, HttpResponseMessage> responder) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            return Task.FromResult(responder(request));
        }
    }
}
