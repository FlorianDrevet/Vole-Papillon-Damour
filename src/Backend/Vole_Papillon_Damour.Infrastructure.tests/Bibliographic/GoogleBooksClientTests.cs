using System.Net;
using FluentAssertions;
using Microsoft.Extensions.Options;
using Vole_Papillon_Damour.Domain.BookAggregate.ValueObjects;
using Vole_Papillon_Damour.Infrastructure.Services.Bibliographic;

namespace Vole_Papillon_Damour.Infrastructure.tests.Bibliographic;

public sealed class GoogleBooksClientTests
{
    [Fact]
    public async Task FindAsync_WithExactIsbnAndImageMapsTheVolumeAndUsesHttps()
    {
        Isbn13.TryCreate("9782070363735", out var isbn13).Should().BeTrue();
        using var httpClient = new HttpClient(new StubHttpMessageHandler(request =>
        {
            if (request.RequestUri?.Host == "books.google.com")
            {
                var imageResponse = new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new ByteArrayContent([0xFF, 0xD8, 0xFF])
                };
                imageResponse.Content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("image/jpeg");
                return imageResponse;
            }

            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(ExactVolumeResponse)
            };
        }));
        var client = new GoogleBooksClient(
            httpClient,
            Options.Create(new BibliographicOptions
            {
                GoogleBooksEndpoint = "https://www.googleapis.com/books/v1/volumes",
                GoogleBooksApiKey = "test-key",
            }));

        var result = await client.FindAsync(isbn13, CancellationToken.None);

        result.Should().NotBeNull();
        result!.Isbn13.Should().Be("9782070363735");
        result.Title.Should().Be("Le Petit Prince");
        result.Authors.Should().Be("Antoine de Saint-Exupéry");
        result.PublicationYear.Should().Be(1946);
        result.CoverUrl.Should().Be("https://books.google.com/books/content?id=volume-42");
        result.CoverSource.Should().Be("GoogleBooks");
    }

    [Fact]
    public async Task FindAsync_WhenOnlyRelatedEditionMatches_ReturnsNullWithoutUsingItsCover()
    {
        Isbn13.TryCreate("9782070363735", out var isbn13).Should().BeTrue();
        var imageRequestCount = 0;
        using var httpClient = new HttpClient(new StubHttpMessageHandler(request =>
        {
            if (request.RequestUri?.Host == "books.google.com")
            {
                imageRequestCount++;
                return new HttpResponseMessage(HttpStatusCode.OK);
            }

            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(RelatedEditionResponse)
            };
        }));
        var client = new GoogleBooksClient(
            httpClient,
            Options.Create(new BibliographicOptions
            {
                GoogleBooksEndpoint = "https://www.googleapis.com/books/v1/volumes"
            }));

        var result = await client.FindAsync(isbn13, CancellationToken.None);

        result.Should().BeNull();
        imageRequestCount.Should().Be(0);
    }

    [Fact]
    public async Task FindAsync_WhenExactVolumeHasNoImage_ReturnsMetadataWithoutCover()
    {
        Isbn13.TryCreate("9782070363735", out var isbn13).Should().BeTrue();
        using var httpClient = new HttpClient(new StubHttpMessageHandler(_ =>
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(NoImageResponse)
            }));
        var client = new GoogleBooksClient(
            httpClient,
            Options.Create(new BibliographicOptions
            {
                GoogleBooksEndpoint = "https://www.googleapis.com/books/v1/volumes"
            }));

        var result = await client.FindAsync(isbn13, CancellationToken.None);

        result.Should().NotBeNull();
        result!.CoverUrl.Should().BeNull();
        result.CoverSource.Should().BeNull();
    }

    private const string ExactVolumeResponse = """
        {
          "items": [
            {
              "id": "volume-42",
              "volumeInfo": {
                "title": "Le Petit Prince",
                "authors": ["Antoine de Saint-Exupéry"],
                "publisher": "Gallimard",
                "publishedDate": "1946-01-01",
                "industryIdentifiers": [{"type":"ISBN_13","identifier":"978-2-07-036373-5"}],
                "imageLinks": {"thumbnail":"http://books.google.com/books/content?id=volume-42"}
              }
            }
          ]
        }
        """;

    private const string RelatedEditionResponse = """
        {
          "items": [
            {
              "id": "related-volume",
              "volumeInfo": {
                "title": "Le Petit Prince",
                "industryIdentifiers": [{"type":"ISBN_13","identifier":"9782728923311"}],
                "imageLinks": {"thumbnail":"http://books.google.com/books/content?id=related-volume"}
              }
            }
          ]
        }
        """;

    private const string NoImageResponse = """
        {
          "items": [
            {
              "id": "volume-42",
              "volumeInfo": {
                "title": "Le Petit Prince",
                "industryIdentifiers": [{"type":"ISBN_13","identifier":"9782070363735"}]
              }
            }
          ]
        }
        """;

    private sealed class StubHttpMessageHandler(
        Func<HttpRequestMessage, HttpResponseMessage> responseFactory) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            return Task.FromResult(responseFactory(request));
        }
    }
}
