using System.Net;
using FluentAssertions;
using Microsoft.Extensions.Options;
using Vole_Papillon_Damour.Application.AccountDeletion;
using Vole_Papillon_Damour.Infrastructure.AccountDeletion;

namespace Vole_Papillon_Damour.Infrastructure.tests.AccountDeletion;

public class EntraGraphUserDirectoryTests
{
    [Fact]
    public async Task DeleteAsync_WhenGraphReturnsNotFound_TreatsTheIdentityAsAlreadyDeleted()
    {
        var requests = new List<HttpRequestMessage>();
        using var client = new HttpClient(new RecordingHandler(request =>
        {
            requests.Add(request);
            return request.Method == HttpMethod.Post
                ? JsonResponse("{\"access_token\":\"access-token\"}")
                : new HttpResponseMessage(HttpStatusCode.NotFound);
        }));
        var options = Options.Create(new EntraGraphOptions
        {
            TenantId = "tenant-id",
            ClientId = "client-id",
            ClientSecret = "client-secret"
        });
        var directory = new EntraGraphUserDirectory(client, options);

        await directory.DeleteAsync("object-id", CancellationToken.None);

        requests.Should().HaveCount(2);
        requests[0].Method.Should().Be(HttpMethod.Post);
        requests[1].Method.Should().Be(HttpMethod.Delete);
        requests[1].RequestUri!.ToString()
            .Should().Be("https://graph.microsoft.com/v1.0/users/object-id");
        var authorization = requests[1].Headers.Authorization;
        authorization.Should().NotBeNull();
        authorization!.Scheme.Should().Be("Bearer");
        authorization.Parameter.Should().Be("access-token");
    }

    [Fact]
    public async Task DeleteAsync_WhenGraphReturnsAnError_ThrowsAReplayableDependencyException()
    {
        using var client = new HttpClient(new RecordingHandler(request => request.Method == HttpMethod.Post
            ? JsonResponse("{\"access_token\":\"access-token\"}")
            : new HttpResponseMessage(HttpStatusCode.ServiceUnavailable)));
        var options = Options.Create(new EntraGraphOptions
        {
            TenantId = "tenant-id",
            ClientId = "client-id",
            ClientSecret = "client-secret"
        });
        var directory = new EntraGraphUserDirectory(client, options);

        var action = () => directory.DeleteAsync("object-id", CancellationToken.None);

        var exception = await action.Should().ThrowAsync<AccountDeletionDependencyException>();
        exception.Which.FailureCode.Should().Be("graph-http-503");
    }

    private static HttpResponseMessage JsonResponse(string json)
    {
        return new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json")
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
