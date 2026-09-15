using System.Net;
using FluentAssertions;
using Microsoft.Extensions.Options;
using Vole_Papillon_Damour.Infrastructure.Services.Bibliographic;

namespace Vole_Papillon_Damour.Infrastructure.tests.Bibliographic;

public sealed class BnfSruSearchClientTests
{
    [Fact]
    public async Task SearchAsync_WithIsbnQuery_SearchesIsbnIndexAndMapsEditions()
    {
        HttpRequestMessage? capturedRequest = null;
        var client = CreateClient(request =>
        {
            capturedRequest = request;
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(RecordedSearchResponse)
            };
        });

        var result = await client.SearchAsync("978-2-07-036373-5", 1, 20, CancellationToken.None);

        result.Should().ContainSingle();
        result[0].Isbn13.Should().Be("9782070363735");
        result[0].WorkId.Should().BeNull();
        result[0].Title.Should().Be("Le Petit Prince");
        result[0].Authors.Should().Be("Saint-Exupéry, Antoine de");
        result[0].Publisher.Should().Be("Gallimard");
        result[0].PublicationYear.Should().Be(1946);
        result[0].CoverUrl.Should().BeNull();
        result[0].Source.Should().Be("BnF");
        capturedRequest.Should().NotBeNull();
        var query = Uri.UnescapeDataString(capturedRequest!.RequestUri!.Query);
        query.Should().Contain("bib.isbn all \"9782070363735\"");
        query.Should().Contain("recordSchema=unimarcXchange");
        query.Should().Contain("maximumRecords=20");
        query.Should().Contain("startRecord=1");
    }

    [Fact]
    public async Task SearchAsync_WithTextQuery_SearchesAllIndexesWithPagination()
    {
        HttpRequestMessage? capturedRequest = null;
        var client = CreateClient(request =>
        {
            capturedRequest = request;
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(RecordedSearchResponse)
            };
        });

        await client.SearchAsync("Le \"Petit\" Prince", 2, 10, CancellationToken.None);

        capturedRequest.Should().NotBeNull();
        var query = Uri.UnescapeDataString(capturedRequest!.RequestUri!.Query);
        query.Should().Contain("bib.anywhere all \"Le Petit Prince\"");
        query.Should().Contain("maximumRecords=10");
        query.Should().Contain("startRecord=11");
    }

    [Fact]
    public async Task SearchAsync_WhenSourceReturnsAnError_ReturnsNoEdition()
    {
        var client = CreateClient(_ => new HttpResponseMessage(HttpStatusCode.ServiceUnavailable));

        var result = await client.SearchAsync("Petit Prince", 1, 20, CancellationToken.None);

        result.Should().BeEmpty();
    }

    private static BnfSruSearchClient CreateClient(Func<HttpRequestMessage, HttpResponseMessage> responseFactory)
    {
        return new BnfSruSearchClient(
            new HttpClient(new StubHttpMessageHandler(responseFactory)),
            Options.Create(new BibliographicOptions
            {
                BnfSruEndpoint = "https://bnf.example.test/api/SRU"
            }));
    }

    private const string RecordedSearchResponse = """
        <?xml version="1.0" encoding="UTF-8"?>
        <srw:searchRetrieveResponse xmlns:srw="http://www.loc.gov/zing/srw/" xmlns:mxc="http://www.bnf.fr/namespaces/marcxchange/">
          <srw:numberOfRecords>3</srw:numberOfRecords>
          <srw:records>
            <srw:record>
              <srw:recordData>
                <mxc:record>
                  <mxc:datafield tag="010"><mxc:subfield code="a">978-2-07-036373-5</mxc:subfield></mxc:datafield>
                  <mxc:datafield tag="200"><mxc:subfield code="a">Le Petit Prince</mxc:subfield></mxc:datafield>
                  <mxc:datafield tag="700">
                    <mxc:subfield code="a">Saint-Exupéry</mxc:subfield>
                    <mxc:subfield code="b">Antoine de</mxc:subfield>
                  </mxc:datafield>
                  <mxc:datafield tag="210">
                    <mxc:subfield code="c">Gallimard</mxc:subfield>
                    <mxc:subfield code="d">1946</mxc:subfield>
                  </mxc:datafield>
                </mxc:record>
              </srw:recordData>
            </srw:record>
            <srw:record>
              <srw:recordData>
                <mxc:record>
                  <mxc:datafield tag="010"><mxc:subfield code="a">9782070363735</mxc:subfield></mxc:datafield>
                  <mxc:datafield tag="200"><mxc:subfield code="a">Le Petit Prince (doublon)</mxc:subfield></mxc:datafield>
                </mxc:record>
              </srw:recordData>
            </srw:record>
            <srw:record>
              <srw:recordData>
                <mxc:record>
                  <mxc:datafield tag="200"><mxc:subfield code="a">Notice sans ISBN</mxc:subfield></mxc:datafield>
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
