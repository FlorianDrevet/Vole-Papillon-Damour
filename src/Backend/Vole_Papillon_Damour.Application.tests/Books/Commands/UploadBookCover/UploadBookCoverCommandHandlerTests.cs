using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Vole_Papillon_Damour.Application.Books.Commands.UploadBookCover;
using Vole_Papillon_Damour.Application.Common.Interfaces.Services;
using Vole_Papillon_Damour.Application.tests.Books.Commands.ScanBook;
using Vole_Papillon_Damour.Domain.BookAggregate.ValueObjects;
using Vole_Papillon_Damour.Domain.UserAggregate.ValueObjects;

namespace Vole_Papillon_Damour.Application.tests.Books.Commands.UploadBookCover;

public sealed class UploadBookCoverCommandHandlerTests
{
    private static readonly UserId AdministratorId =
        UserId.Create(Guid.Parse("00000000-0000-0000-0000-000000000002"));

    private static readonly DateTime UpdatedAt =
        new(2026, 10, 20, 15, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task Handle_WhenCoverIsUploaded_StoresBlobUrlAsManualCover()
    {
        await using var fixture = await ScanBookFixture.CreateAsync();
        await fixture.AddBookAsync("9782070363735", quantityAvailable: 1);
        var blobUri = new Uri("https://storage.example.test/book-covers/9782070363735/cover.jpg");
        var blobService = Substitute.For<IBlobService>();
        blobService.UploadBookCoverAsync(
                Arg.Any<string>(),
                Arg.Any<Stream>(),
                "image/jpeg",
                Arg.Any<CancellationToken>())
            .Returns(blobUri);
        var clock = Substitute.For<IDateTimeProvider>();
        clock.UtcNow.Returns(UpdatedAt);
        var handler = new UploadBookCoverCommandHandler(
            fixture.Context,
            blobService,
            clock,
            NullLogger<UploadBookCoverCommandHandler>.Instance);
        await using var stream = new MemoryStream([0xff, 0xd8, 0xff]);

        var result = await handler.Handle(
            new UploadBookCoverCommand(
                "9782070363735",
                stream,
                "image/jpeg",
                3,
                AdministratorId),
            CancellationToken.None);

        result.IsError.Should().BeFalse();
        result.Value.CoverUrl.Should().Be(blobUri);
        var savedBook = await fixture.Context.Books.SingleAsync();
        savedBook.CoverUrl.Should().Be(blobUri.ToString());
        savedBook.CoverSource.Should().Be(BookCoverSource.Manual);
        savedBook.ManuallyEditedFields.Should().Be("[\"CoverUrl\"]");
        savedBook.MetadataStatus.Should().Be(BookMetadataStatus.Manual);
    }

    [Fact]
    public async Task Handle_WhenImageTypeIsUnsupported_ReturnsValidationErrorWithoutUploading()
    {
        await using var fixture = await ScanBookFixture.CreateAsync();
        await fixture.AddBookAsync("9782070363735", quantityAvailable: 1);
        var blobService = Substitute.For<IBlobService>();
        var clock = Substitute.For<IDateTimeProvider>();
        clock.UtcNow.Returns(UpdatedAt);
        var handler = new UploadBookCoverCommandHandler(
            fixture.Context,
            blobService,
            clock,
            NullLogger<UploadBookCoverCommandHandler>.Instance);
        await using var stream = new MemoryStream([1, 2, 3]);

        var result = await handler.Handle(
            new UploadBookCoverCommand(
                "9782070363735",
                stream,
                "application/pdf",
                3,
                AdministratorId),
            CancellationToken.None);

        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("Book.InvalidCoverType");
        await blobService.DidNotReceive().UploadBookCoverAsync(
            Arg.Any<string>(),
            Arg.Any<Stream>(),
            Arg.Any<string>(),
            Arg.Any<CancellationToken>());
    }
}
