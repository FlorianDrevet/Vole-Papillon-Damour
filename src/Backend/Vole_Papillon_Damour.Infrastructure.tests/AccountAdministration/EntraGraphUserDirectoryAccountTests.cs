using System.Net;
using System.Text;
using FluentAssertions;
using Microsoft.Extensions.Options;
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
            "Marie Tri",
            "Temporaire1!",
            ["Tri"],
            CancellationToken.None);

        account.ExternalId.Should().Be("account-1");
        account.Roles.Should().ContainSingle().Which.Should().Be("Tri");
        requests.Should().Contain(request =>
            request.Method == HttpMethod.Post &&
            request.RequestUri!.AbsolutePath == "/v1.0/users/account-1/appRoleAssignments");
        var createBody = createBodies.Single();
        createBody.Should().Contain("\"issuer\":\"volepapillondamour.onmicrosoft.com\"");
        createBody.Should().Contain("\"issuerAssignedId\":\"marie@example.test\"");
        createBody.Should().Contain("\"forceChangePasswordNextSignIn\":true");
        createBody.Should().Contain("\"passwordPolicies\":\"DisablePasswordExpiration\"");
    }

    private static HttpResponseMessage JsonResponse(string json)
    {
        return new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        };
    }

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
