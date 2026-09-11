using System.Net;
using System.Net.Http.Headers;
using FluentAssertions;
using Microsoft.Extensions.Options;
using Vole_Papillon_Damour.Infrastructure.Services.Social;

namespace Vole_Papillon_Damour.Infrastructure.tests.Social;

public sealed class InstagramFeedClientTests
{
    [Fact]
    public async Task GetRecentPostsAsync_MapsSinglePostsAndCarouselChildrenInOrder()
    {
        var handler = new StubHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("""
                {
                  "data": [
                    {
                      "id": "single-1",
                      "caption": "Une publication",
                      "media_type": "IMAGE",
                      "media_url": "https://cdn.example.test/single.jpg",
                      "permalink": "https://www.instagram.com/p/single-1/",
                      "timestamp": "2026-09-10T08:30:00+0000"
                    },
                    {
                      "id": "carousel-1",
                      "caption": "Un carrousel",
                      "media_type": "CAROUSEL_ALBUM",
                      "permalink": "https://www.instagram.com/p/carousel-1/",
                      "timestamp": "2026-09-09T08:30:00+0000",
                      "children": {
                        "data": [
                          {
                            "id": "child-1",
                            "media_type": "IMAGE",
                            "media_url": "https://cdn.example.test/one.jpg"
                          },
                          {
                            "id": "child-2",
                            "media_type": "VIDEO",
                            "media_url": "https://cdn.example.test/two.mp4",
                            "thumbnail_url": "https://cdn.example.test/two-thumb.jpg"
                          }
                        ]
                      }
                    }
                  ],
                  "paging": { "next": "https://graph.instagram.com/next" }
                }
                """)
        });
        using var httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://graph.instagram.com/")
        };
        var options = Options.Create(new InstagramOptions
        {
            GraphApiVersion = "v22.0",
            UserId = "account-123",
            AccessToken = "secret-token",
        });
        var client = new InstagramFeedClient(httpClient, options);

        var posts = await client.GetRecentPostsAsync(CancellationToken.None);

        posts.Should().HaveCount(2);
        posts[0].Medias.Should().ContainSingle();
        posts[1].Medias.Should().HaveCount(2);
        posts[1].Medias[0].ContentUrl.Should().Be(new Uri("https://cdn.example.test/one.jpg"));
        posts[1].Medias[1].ThumbnailUrl.Should().Be(new Uri("https://cdn.example.test/two-thumb.jpg"));
        handler.LastRequest!.RequestUri!.Query.Should().Contain("fields=");
        handler.LastRequest.Headers.Authorization.Should().Be(
            new AuthenticationHeaderValue("Bearer", "secret-token"));
        handler.LastRequest.RequestUri.AbsolutePath.Should().Be("/v22.0/account-123/media");
    }

    [Fact]
    public async Task GetRecentPostsAsync_RemovesCopyFormattingAroundAccessTokenBeforeSendingRequest()
    {
        var handler = new StubHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("{\"data\":[]}")
        });
        using var httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://graph.instagram.com/")
        };
        var options = Options.Create(new InstagramOptions
        {
            GraphApiVersion = "v22.0",
            UserId = "account-123",
            AccessToken = " \u200Bsecret-token\uFEFF "
        });
        var client = new InstagramFeedClient(httpClient, options);

        await client.GetRecentPostsAsync(CancellationToken.None);

        handler.LastRequest!.Headers.Authorization.Should().Be(
            new AuthenticationHeaderValue("Bearer", "secret-token"));
    }

    [Fact]
    public async Task GetRecentPostsAsync_RejectsAnAccessTokenWithInternalNonAsciiCharacters()
    {
        var handler = new StubHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("{\"data\":[]}")
        });
        using var httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://graph.instagram.com/")
        };
        var options = Options.Create(new InstagramOptions
        {
            GraphApiVersion = "v22.0",
            UserId = "account-123",
            AccessToken = "secret-\u200B-token"
        });
        var client = new InstagramFeedClient(httpClient, options);

        var act = () => client.GetRecentPostsAsync(CancellationToken.None);

        await act.Should().ThrowAsync<SocialFeedAuthenticationException>()
            .WithMessage("The Instagram access token contains invalid characters*");
        handler.LastRequest.Should().BeNull();
    }

    [Fact]
    public async Task GetRecentPostsAsync_ThrowsAnAuthenticationExceptionForAnExpiredToken()
    {
        var client = CreateClient(
            HttpStatusCode.Unauthorized,
            "{\"error\":{\"message\":\"Invalid OAuth access token\",\"code\":190}}");

        var act = () => client.GetRecentPostsAsync(CancellationToken.None);

        await act.Should().ThrowAsync<SocialFeedAuthenticationException>();
    }

    [Fact]
    public async Task GetRecentPostsAsync_ThrowsAQuotaExceptionWhenMetaThrottlesTheRequest()
    {
        var client = CreateClient(
            (HttpStatusCode)429,
            "{\"error\":{\"message\":\"Application request limit reached\",\"code\":4}}");

        var act = () => client.GetRecentPostsAsync(CancellationToken.None);

        await act.Should().ThrowAsync<SocialFeedQuotaException>();
    }

    private static InstagramFeedClient CreateClient(HttpStatusCode statusCode, string body)
    {
        var handler = new StubHttpMessageHandler(_ => new HttpResponseMessage(statusCode)
        {
            Content = new StringContent(body)
        });
        return new InstagramFeedClient(
            new HttpClient(handler)
            {
                BaseAddress = new Uri("https://graph.instagram.com/")
            },
            Options.Create(new InstagramOptions
            {
                GraphApiVersion = "v22.0",
                UserId = "account-123",
                AccessToken = "secret-token",
            }));
    }

    private sealed class StubHttpMessageHandler(
        Func<HttpRequestMessage, HttpResponseMessage> responseFactory) : HttpMessageHandler
    {
        public HttpRequestMessage? LastRequest { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            LastRequest = request;
            return Task.FromResult(responseFactory(request));
        }
    }
}
