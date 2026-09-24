using System.Net;
using FluentAssertions;
using Microsoft.Extensions.Options;
using Vole_Papillon_Damour.Infrastructure.Services.Bibliographic;
using Vole_Papillon_Damour.Infrastructure.Services.Recommendations;

namespace Vole_Papillon_Damour.Infrastructure.tests.Recommendations;

public sealed class BibliographicNoticeReaderTests
{
    [Fact]
    public async Task ReadAsync_UsesFuzzyIsbnAndUnimarcSchema()
    {
        Uri? bnfRequest = null;
        using var client = CreateClient(request =>
        {
            if (request.RequestUri?.Host == "bnf.example.test")
            {
                bnfRequest = request.RequestUri;
                return XmlResponse(RecordedBnfNotice);
            }

            return new HttpResponseMessage(HttpStatusCode.NotFound);
        });
        var reader = CreateReader(client);

        var edition = await reader.ReadAsync("9782742793099", CancellationToken.None);

        edition.Should().NotBeNull();
        bnfRequest.Should().NotBeNull();
        var query = Uri.UnescapeDataString(bnfRequest!.Query);
        query.Should().Contain("bib.fuzzyISBN all \"9782742793099\"");
        query.Should().Contain("recordSchema=unimarcXchange");
    }

    [Theory]
    [InlineData("\"Description de l'œuvre issue d'Open Library.\"", "Description de l'œuvre issue d'Open Library.")]
    [InlineData("{\"value\":\"Description de l'œuvre issue d'Open Library.\"}", "Description de l'œuvre issue d'Open Library.")]
    public async Task ReadAsync_UsesOpenLibraryWorkDescriptionWhenEditionHasNone(string workDescription, string expectedDescription)
    {
        var requestedWork = false;
        using var client = CreateClient(request =>
        {
            var path = request.RequestUri!.AbsolutePath;
            if (path.Contains("/api/SRU", StringComparison.Ordinal))
            {
                return XmlResponse(RecordedBnfNotice);
            }

            if (path == "/isbn/9782742793099.json")
            {
                return JsonResponse("""
                    {"title":"Les hommes qui n'aimaient pas les femmes","works":[{"key":"/works/OL123W"}]}
                    """);
            }

            requestedWork = true;
            path.Should().Be("/works/OL123W.json");
            return JsonResponse($"{{\"description\":{workDescription}}}");
        });
        var reader = CreateReader(client);

        var edition = await reader.ReadAsync("9782742793099", CancellationToken.None);

        requestedWork.Should().BeTrue();
        edition!.OpenLibraryWorkKey.Should().Be("/works/OL123W");
        edition.OpenLibraryDescription.Should().Be(expectedDescription);
    }

    [Fact]
    public async Task ReadAsync_ReturnsNullWhenNeitherSourceKnowsTheIsbn()
    {
        using var client = CreateClient(request => request.RequestUri!.Host == "bnf.example.test"
            ? XmlResponse("<searchRetrieveResponse><numberOfRecords>0</numberOfRecords></searchRetrieveResponse>")
            : new HttpResponseMessage(HttpStatusCode.NotFound));
        var reader = CreateReader(client);

        var edition = await reader.ReadAsync("9782742793099", CancellationToken.None);

        edition.Should().BeNull();
    }

    [Fact]
    public async Task ReadAsync_UsesOpenLibraryWhenBnfReturnsServerError()
    {
        using var client = CreateClient(request => request.RequestUri!.Host == "bnf.example.test"
            ? new HttpResponseMessage(HttpStatusCode.ServiceUnavailable)
            : JsonResponse("""
                {"title":"Titre Open Library","description":"Description Open Library","works":[{"key":"/works/OL456W"}]}
                """));
        var reader = CreateReader(client);

        var edition = await reader.ReadAsync("9782742793099", CancellationToken.None);

        edition.Should().NotBeNull();
        edition!.Title.Should().Be("Titre Open Library");
        edition.Authors.Should().BeEmpty();
        edition.OpenLibraryWorkKey.Should().Be("/works/OL456W");
        edition.OpenLibraryDescription.Should().Be("Description Open Library");
    }

    private static BibliographicNoticeReader CreateReader(HttpClient client) => new(
        client,
        Options.Create(new BibliographicOptions
        {
            BnfSruEndpoint = "https://bnf.example.test/api/SRU",
        }));

    private static HttpClient CreateClient(Func<HttpRequestMessage, HttpResponseMessage> responseFactory) =>
        new(new StubHttpMessageHandler(responseFactory));

    private static HttpResponseMessage XmlResponse(string xml) => new(HttpStatusCode.OK)
    {
        Content = new StringContent(xml),
    };

    private static HttpResponseMessage JsonResponse(string json) => new(HttpStatusCode.OK)
    {
        Content = new StringContent(json),
    };

    private const string RecordedBnfNotice = """
        <srw:searchRetrieveResponse xmlns:srw="http://www.loc.gov/zing/srw/" xmlns:mxc="http://www.bnf.fr/namespaces/marcxchange/">
          <srw:numberOfRecords>1</srw:numberOfRecords>
          <srw:records><srw:record><srw:recordData>
            <mxc:record>
              <mxc:datafield tag="010"><mxc:subfield code="a">9782742793099</mxc:subfield></mxc:datafield>
              <mxc:datafield tag="200"><mxc:subfield code="a">Les hommes qui n'aimaient pas les femmes</mxc:subfield></mxc:datafield>
            </mxc:record>
          </srw:recordData></srw:record></srw:records>
        </srw:searchRetrieveResponse>
        """;

    private sealed class StubHttpMessageHandler(
        Func<HttpRequestMessage, HttpResponseMessage> responseFactory) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken) => Task.FromResult(responseFactory(request));
    }
}
