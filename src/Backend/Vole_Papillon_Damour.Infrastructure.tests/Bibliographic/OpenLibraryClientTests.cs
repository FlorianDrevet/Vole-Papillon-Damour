using System.Net;
using FluentAssertions;
using Microsoft.Extensions.Options;
using Vole_Papillon_Damour.Domain.BookAggregate.ValueObjects;
using Vole_Papillon_Damour.Infrastructure.Services.Bibliographic;

namespace Vole_Papillon_Damour.Infrastructure.tests.Bibliographic;

public sealed class OpenLibraryClientTests
{
    [Fact]
    public async Task FindAsync_WithRecordedSearchResponse_MapsBookMetadata()
    {
        Isbn13.TryCreate("9782070363735", out var isbn13).Should().BeTrue();
        HttpRequestMessage? capturedRequest = null;
        using var httpClient = new HttpClient(new StubHttpMessageHandler(request =>
        {
            if (request.RequestUri?.Host == "covers.openlibrary.org")
            {
                var coverResponse = new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new ByteArrayContent([0xFF, 0xD8, 0xFF])
                };
                coverResponse.Content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("image/jpeg");
                return coverResponse;
            }

            capturedRequest = request;
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(RecordedResponse)
            };
        }));
        var client = new OpenLibraryClient(
            httpClient,
            Options.Create(new BibliographicOptions
            {
                OpenLibrarySearchEndpoint = "https://openlibrary.example.test/search.json"
            }));

        var result = await client.FindAsync(isbn13, CancellationToken.None);

        result.Should().NotBeNull();
        result!.Title.Should().Be("Le Petit Prince");
        result.Authors.Should().Be("Antoine de Saint-Exupéry");
        result.Publisher.Should().Be("Gallimard");
        result.PublicationYear.Should().Be(1946);
        result.WorkId.Should().Be("OL123W");
        result.CoverUrl.Should().Be("https://covers.openlibrary.org/b/id/12345-L.jpg?default=false");
        result.CoverSource.Should().Be("OpenLibrary");
        result.Source.Should().Be("OpenLibrary");
        capturedRequest.Should().NotBeNull();
        capturedRequest!.RequestUri!.Query.Should().Contain("isbn");
        capturedRequest.RequestUri.Query.Should().Contain("9782070363735");
    }

    [Fact]
    public async Task FindAsync_WhenCoverIdIsMissing_ReturnsMetadataWithoutACover()
    {
        Isbn13.TryCreate("9782070363735", out var isbn13).Should().BeTrue();
        using var httpClient = new HttpClient(new StubHttpMessageHandler(_ =>
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("""{"numFound":1,"docs":[{"title":"Le Petit Prince"}]}""")
            }));
        var client = new OpenLibraryClient(
            httpClient,
            Options.Create(new BibliographicOptions
            {
                OpenLibrarySearchEndpoint = "https://openlibrary.example.test/search.json"
            }));

        var result = await client.FindAsync(isbn13, CancellationToken.None);

        result.Should().NotBeNull();
        result!.CoverUrl.Should().BeNull();
        result.CoverSource.Should().BeNull();
    }

    [Fact]
    public async Task FindAsync_WhenSearchHasNoDocuments_ReturnsNull()
    {
        Isbn13.TryCreate("9782070363735", out var isbn13).Should().BeTrue();
        using var httpClient = new HttpClient(new StubHttpMessageHandler(_ =>
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("{\"numFound\":0,\"docs\":[]}")
            }));
        var client = new OpenLibraryClient(
            httpClient,
            Options.Create(new BibliographicOptions
            {
                OpenLibrarySearchEndpoint = "https://openlibrary.example.test/search.json"
            }));

        var result = await client.FindAsync(isbn13, CancellationToken.None);

        result.Should().BeNull();
    }

    private const string RecordedResponse = """
        {
          "numFound": 1,
          "docs": [
            {
              "title": "Le Petit Prince",
              "author_name": ["Antoine de Saint-Exupéry"],
              "publisher": ["Gallimard"],
              "first_publish_year": 1946,
              "cover_i": 12345,
              "key": "/works/OL123W"
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
