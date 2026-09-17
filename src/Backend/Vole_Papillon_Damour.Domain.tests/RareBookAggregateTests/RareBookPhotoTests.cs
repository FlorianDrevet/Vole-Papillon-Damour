using FluentAssertions;
using Vole_Papillon_Damour.Domain.RareBookAggregate.Entities;
using Vole_Papillon_Damour.Domain.RareBookAggregate.ValueObjects;
using Vole_Papillon_Damour.Domain.UserAggregate.ValueObjects;

namespace Vole_Papillon_Damour.Domain.tests.RareBookAggregateTests;

public sealed class RareBookPhotoTests
{
    private static readonly DateTime UploadedAt = new(2026, 9, 16, 10, 0, 0, DateTimeKind.Utc);
    private static readonly RareBookId RareBookId = RareBookId.Create(
        Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"));
    private static readonly UserId UploadedBy = UserId.Create(
        Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb"));

    [Fact]
    public void Create_WithValidHttpsJpeg_StoresBlobMetadata()
    {
        var photo = RareBookPhoto.Create(
            RareBookId,
            new Uri("https://storage.example.test/livres-rares/cover.jpg"),
            "cover.jpg",
            "image/jpeg",
            2048,
            UploadedAt,
            UploadedBy,
            caption: "Page de titre");

        photo.Id.Value.Should().NotBeEmpty();
        photo.RareBookId.Should().Be(RareBookId);
        photo.BlobName.Should().Be("cover.jpg");
        photo.BlobUri.AbsoluteUri.Should().Be("https://storage.example.test/livres-rares/cover.jpg");
        photo.Caption.Should().Be("Page de titre");
        photo.Position.Should().Be(0);
        photo.ContentType.Should().Be("image/jpeg");
        photo.SizeBytes.Should().Be(2048);
        photo.UploadedAt.Should().Be(UploadedAt);
        photo.UploadedBy.Should().Be(UploadedBy);
    }

    [Fact]
    public void Create_WithHttpUriOrUnsupportedContentType_Throws()
    {
        var httpUri = () => RareBookPhoto.Create(
            RareBookId,
            new Uri("http://storage.example.test/cover.jpg"),
            "cover.jpg",
            "image/jpeg",
            2048,
            UploadedAt,
            UploadedBy);
        var unsupportedContentType = () => RareBookPhoto.Create(
            RareBookId,
            new Uri("https://storage.example.test/cover.gif"),
            "cover.gif",
            "image/gif",
            2048,
            UploadedAt,
            UploadedBy);

        httpUri.Should().Throw<ArgumentException>();
        unsupportedContentType.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Create_WithPng_IsSupportedForUpload()
    {
        var photo = RareBookPhoto.Create(
            RareBookId,
            new Uri("https://storage.example.test/cover.png"),
            "cover.png",
            "image/png",
            2048,
            UploadedAt,
            UploadedBy);

        photo.ContentType.Should().Be("image/png");
    }
}
