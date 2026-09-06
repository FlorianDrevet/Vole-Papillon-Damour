using System.Net;
using FluentAssertions;
using Microsoft.Extensions.Options;
using Vole_Papillon_Damour.Domain.BookAggregate.ValueObjects;
using Vole_Papillon_Damour.Infrastructure.Services.Bibliographic;

namespace Vole_Papillon_Damour.Infrastructure.tests.Bibliographic;

public sealed class BnfSruClientTests
{
    [Fact]
    public async Task FindAsync_WithRecordedUnimarcNoticeWithCover_MapsBookMetadata()
    {
        Isbn13.TryCreate("9782070363735", out var isbn13).Should().BeTrue();
        HttpRequestMessage? capturedRequest = null;
        using var httpClient = new HttpClient(new StubHttpMessageHandler(request =>
        {
            if (request.RequestUri?.Host == "openapi.bnf.fr")
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
                Content = new StringContent(RecordedNoticeWithCover)
            };
        }));
        var client = new BnfSruClient(
            httpClient,
            Options.Create(new BibliographicOptions
            {
                BnfSruEndpoint = "https://bnf.example.test/api/SRU"
            }));

        var result = await client.FindAsync(isbn13, CancellationToken.None);

        result.Should().NotBeNull();
        result!.Isbn13.Should().Be("9782070363735");
        result.Title.Should().Be("Le Petit Prince");
        result.Authors.Should().Be("Saint-Exupéry, Antoine de");
        result.Publisher.Should().Be("Gallimard");
        result.PublicationYear.Should().Be(1946);
        result.Source.Should().Be("BnF");
        result.CoverUrl.Should().Be("https://openapi.bnf.fr/couverture/image/image/recupererImage?ISBN=9782070363735&couverture=1");
        result.CoverSource.Should().Be("BnF");
        capturedRequest.Should().NotBeNull();
        capturedRequest!.RequestUri!.Query.Should().Contain("bib.isbn");
        capturedRequest.RequestUri.Query.Should().Contain("9782070363735");
        capturedRequest.RequestUri.Query.Should().Contain("recordSchema=unimarcXchange");
    }

    [Fact]
    public async Task FindAsync_WhenCoverEndpointReturnsServerError_DropsTheUnusableCoverUrl()
    {
        Isbn13.TryCreate("9782070363735", out var isbn13).Should().BeTrue();
        var requestCount = 0;
        Uri? coverRequestUri = null;
        using var httpClient = new HttpClient(new StubHttpMessageHandler(request =>
        {
            requestCount++;
            if (request.RequestUri?.Host == "openapi.bnf.fr")
            {
                coverRequestUri = request.RequestUri;
                return new HttpResponseMessage(HttpStatusCode.InternalServerError);
            }

            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(RecordedNoticeWithoutCover)
            };
        }));
        var client = new BnfSruClient(
            httpClient,
            Options.Create(new BibliographicOptions
            {
                BnfSruEndpoint = "https://bnf.example.test/api/SRU"
            }));

        var result = await client.FindAsync(isbn13, CancellationToken.None);

        result.Should().NotBeNull();
        result!.CoverUrl.Should().BeNull();
        result.CoverSource.Should().BeNull();
        requestCount.Should().Be(2);
        coverRequestUri.Should().NotBeNull();
        coverRequestUri!.Query.Should().Contain("ISBN=9782070363735");
        coverRequestUri.Query.Should().Contain("couverture=1");
    }

    [Fact]
    public async Task FindAsync_WhenSourceReturnsNoRecords_ReturnsNull()
    {
        Isbn13.TryCreate("9782070363735", out var isbn13).Should().BeTrue();
        using var httpClient = new HttpClient(new StubHttpMessageHandler(_ =>
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("<searchRetrieveResponse><numberOfRecords>0</numberOfRecords></searchRetrieveResponse>")
            }));
        var client = new BnfSruClient(
            httpClient,
            Options.Create(new BibliographicOptions
            {
                BnfSruEndpoint = "https://bnf.example.test/api/SRU"
            }));

        var result = await client.FindAsync(isbn13, CancellationToken.None);

        result.Should().BeNull();
    }

    private const string RecordedNoticeWithCover = """
        <?xml version="1.0" encoding="UTF-8"?>
        <srw:searchRetrieveResponse xmlns:srw="http://www.loc.gov/zing/srw/" xmlns:mxc="http://www.bnf.fr/namespaces/marcxchange/">
          <srw:numberOfRecords>1</srw:numberOfRecords>
          <srw:records>
            <srw:record>
              <srw:recordData>
                <mxc:record>
                  <mxc:datafield tag="010"><mxc:subfield code="a">9782070363735</mxc:subfield></mxc:datafield>
                  <mxc:datafield tag="200"><mxc:subfield code="a">Le Petit Prince</mxc:subfield></mxc:datafield>
                  <mxc:datafield tag="700">
                    <mxc:subfield code="a">Saint-Exupéry</mxc:subfield>
                    <mxc:subfield code="b">Antoine de</mxc:subfield>
                  </mxc:datafield>
                  <mxc:datafield tag="210">
                    <mxc:subfield code="c">Gallimard</mxc:subfield>
                    <mxc:subfield code="d">1946</mxc:subfield>
                  </mxc:datafield>
                  <mxc:datafield tag="856">
                    <mxc:subfield code="b">Première de couverture</mxc:subfield>
                    <mxc:subfield code="u">119563</mxc:subfield>
                  </mxc:datafield>
                </mxc:record>
              </srw:recordData>
            </srw:record>
          </srw:records>
        </srw:searchRetrieveResponse>
        """;

    private const string RecordedNoticeWithoutCover = """
        <?xml version="1.0" encoding="UTF-8"?>
        <srw:searchRetrieveResponse xmlns:srw="http://www.loc.gov/zing/srw/" xmlns:mxc="http://www.bnf.fr/namespaces/marcxchange/">
          <srw:numberOfRecords>1</srw:numberOfRecords>
          <srw:records>
            <srw:record>
              <srw:recordData>
                <mxc:record>
                  <mxc:datafield tag="010"><mxc:subfield code="a">9782070363735</mxc:subfield></mxc:datafield>
                  <mxc:datafield tag="200"><mxc:subfield code="a">Le Petit Prince</mxc:subfield></mxc:datafield>
                </mxc:record>
              </srw:recordData>
            </srw:record>
          </srw:records>
        </srw:searchRetrieveResponse>
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
