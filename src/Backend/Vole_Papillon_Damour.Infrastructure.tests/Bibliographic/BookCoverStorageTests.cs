using System.Net;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using Vole_Papillon_Damour.Application.Common.Interfaces.Services;
using Vole_Papillon_Damour.Domain.BookAggregate.ValueObjects;
using Vole_Papillon_Damour.Infrastructure.Services.Bibliographic;

namespace Vole_Papillon_Damour.Infrastructure.tests.Bibliographic;

public sealed class BookCoverStorageTests
{
    [Fact]
    public async Task TryStoreAsync_WithAllowedImage_UploadsStableBookCoverBlob()
    {
        Isbn13.TryCreate("9782070363735", out var isbn13).Should().BeTrue();
        var blobService = Substitute.For<IBlobService>();
        var storedUri = new Uri("https://storage.example.test/book-covers/books/covers/9782070363735.jpg");
        blobService
            .UploadBookCoverAsync(
                "books/covers/9782070363735.jpg",
                Arg.Any<Stream>(),
                "image/jpeg")
            .Returns(storedUri);

        using var httpClient = new HttpClient(new StubHttpMessageHandler(_ =>
        {
            var response = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new ByteArrayContent([1, 2, 3, 4])
            };
            response.Content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("image/jpeg");
            return response;
        }));
        var storage = new BookCoverStorage(
            httpClient,
            blobService,
            Options.Create(new BookCoverOptions()),
            NullLogger<BookCoverStorage>.Instance);

        var result = await storage.TryStoreAsync(
            isbn13,
            new Uri("https://covers.openlibrary.org/b/id/12345-L.jpg"),
            CancellationToken.None);

        result.Should().Be(storedUri);
        await blobService.Received(1).UploadBookCoverAsync(
            "books/covers/9782070363735.jpg",
            Arg.Any<Stream>(),
            "image/jpeg");
    }

    [Fact]
    public async Task TryStoreAsync_WithDisallowedHost_DoesNotMakeRequestOrUpload()
    {
        Isbn13.TryCreate("9782070363735", out var isbn13).Should().BeTrue();
        var blobService = Substitute.For<IBlobService>();
        var requestCount = 0;
        using var httpClient = new HttpClient(new StubHttpMessageHandler(_ =>
        {
            requestCount++;
            return new HttpResponseMessage(HttpStatusCode.OK);
        }));
        var storage = new BookCoverStorage(
            httpClient,
            blobService,
            Options.Create(new BookCoverOptions()),
            NullLogger<BookCoverStorage>.Instance);

        var result = await storage.TryStoreAsync(
            isbn13,
            new Uri("https://attacker.example.test/cover.jpg"),
            CancellationToken.None);

        result.Should().BeNull();
        requestCount.Should().Be(0);
        await blobService.DidNotReceiveWithAnyArgs().UploadBookCoverAsync(default!, default!, default!);
    }

    [Fact]
    public async Task TryStoreAsync_WhenResponseExceedsLimit_DoesNotUpload()
    {
        Isbn13.TryCreate("9782070363735", out var isbn13).Should().BeTrue();
        var blobService = Substitute.For<IBlobService>();
        using var httpClient = new HttpClient(new StubHttpMessageHandler(_ =>
        {
            var response = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new ByteArrayContent([1, 2, 3, 4])
            };
            response.Content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("image/jpeg");
            return response;
        }));
        var storage = new BookCoverStorage(
            httpClient,
            blobService,
            Options.Create(new BookCoverOptions { MaxBytes = 3 }),
            NullLogger<BookCoverStorage>.Instance);

        var result = await storage.TryStoreAsync(
            isbn13,
            new Uri("https://openapi.bnf.fr/couverture/image/image/cover"),
            CancellationToken.None);

        result.Should().BeNull();
        await blobService.DidNotReceiveWithAnyArgs().UploadBookCoverAsync(default!, default!, default!);
    }

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
