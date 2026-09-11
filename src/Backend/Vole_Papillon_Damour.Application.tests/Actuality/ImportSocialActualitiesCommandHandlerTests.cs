using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using Vole_Papillon_Damour.Application.Actuality.Commands.Background;
using Vole_Papillon_Damour.Application.Common.Interfaces.Persistence;
using Vole_Papillon_Damour.Application.Common.Interfaces.Services;
using Vole_Papillon_Damour.Application.Common.Models;
using Vole_Papillon_Damour.Domain.ActualityAggregate;
using Vole_Papillon_Damour.Domain.ActualityAggregate.ValueObjects;
using ActualityAggregate = Vole_Papillon_Damour.Domain.ActualityAggregate.Actuality;

namespace Vole_Papillon_Damour.Application.tests.Actuality;

public sealed class ImportSocialActualitiesCommandHandlerTests
{
    private static readonly DateTimeOffset FloorDate =
        new(2026, 9, 1, 0, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset ImportedAt =
        new(2026, 9, 10, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Handle_ImportsChronologicallyAndStopsAtTheConfiguredCap()
    {
        var posts = Enumerable.Range(0, 6)
            .Select(index => CreatePost(
                $"post-{index}",
                FloorDate.AddDays(index + 1),
                [new SocialPostMedia(
                    SocialMediaKind.Image,
                    new Uri($"https://cdn.example.test/{index}.jpg"),
                    null)]))
            .Reverse()
            .ToList();
        var fixture = CreateFixture(posts);

        var result = await fixture.Handler.Handle(
            new ImportSocialActualitiesCommand(),
            CancellationToken.None);

        result.ExaminedCount.Should().Be(6);
        result.ImportedCount.Should().Be(5);
        result.AlreadyKnownCount.Should().Be(0);
        fixture.ImportedExternalIds.Should().Equal("post-0", "post-1", "post-2", "post-3", "post-4");
    }

    [Fact]
    public async Task Handle_UsesVideoThumbnailsAndPreservesCarouselOrder()
    {
        var post = CreatePost(
            "carousel-1",
            FloorDate.AddDays(1),
            [
                new SocialPostMedia(
                    SocialMediaKind.Image,
                    new Uri("https://cdn.example.test/first.jpg"),
                    null),
                new SocialPostMedia(
                    SocialMediaKind.Video,
                    new Uri("https://cdn.example.test/video.mp4"),
                    new Uri("https://cdn.example.test/video-thumb.jpg")),
            ]);
        var fixture = CreateFixture([post]);

        var result = await fixture.Handler.Handle(
            new ImportSocialActualitiesCommand(),
            CancellationToken.None);

        result.ImportedCount.Should().Be(1);
        await fixture.Downloader.Received(1).DownloadAsync(
            new Uri("https://cdn.example.test/first.jpg"),
            Arg.Any<CancellationToken>());
        await fixture.Downloader.Received(1).DownloadAsync(
            new Uri("https://cdn.example.test/video-thumb.jpg"),
            Arg.Any<CancellationToken>());
        var actuality = fixture.ImportedActualities.Should().ContainSingle().Which;
        actuality.UrlPrincipalImage.ToString().Should().Contain("-0.jpg");
        actuality.Images.Should().ContainSingle()
            .Which.ToString().Should().Contain("-1.jpg");
    }

    [Fact]
    public async Task Handle_CapsAnActualityAtTenMediaItems()
    {
        var post = CreatePost(
            "large-carousel",
            FloorDate.AddDays(1),
            Enumerable.Range(0, 12)
                .Select(index => new SocialPostMedia(
                    SocialMediaKind.Image,
                    new Uri($"https://cdn.example.test/{index}.jpg"),
                    null))
                .ToList());
        var fixture = CreateFixture([post]);

        var result = await fixture.Handler.Handle(
            new ImportSocialActualitiesCommand(),
            CancellationToken.None);

        result.ImportedCount.Should().Be(1);
        await fixture.Downloader.Received(10).DownloadAsync(
            Arg.Any<Uri>(),
            Arg.Any<CancellationToken>());
        await fixture.Blob.Received(10).UploadActualityImagesAsync(
            Arg.Any<string>(),
            Arg.Any<Stream>());
        fixture.ImportedActualities.Should().ContainSingle()
            .Which.Images.Should().HaveCount(9);
    }

    [Fact]
    public async Task Handle_IsIdempotentAndDoesNotReimportADeletedActuality()
    {
        var post = CreatePost(
            "post-once",
            FloorDate.AddDays(1),
            [new SocialPostMedia(
                SocialMediaKind.Image,
                new Uri("https://cdn.example.test/once.jpg"),
                null)]);
        var fixture = CreateFixture([post]);

        var first = await fixture.Handler.Handle(
            new ImportSocialActualitiesCommand(),
            CancellationToken.None);
        var second = await fixture.Handler.Handle(
            new ImportSocialActualitiesCommand(),
            CancellationToken.None);

        first.ImportedCount.Should().Be(1);
        second.ImportedCount.Should().Be(0);
        second.AlreadyKnownCount.Should().Be(1);
        fixture.ImportedActualities.Should().ContainSingle();
        await fixture.Store.Received(1).PersistAsync(
            Arg.Any<ActualityAggregate>(),
            Arg.Any<SocialPostImport>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_PublishesImportedActualityImmediately()
    {
        var post = CreatePost(
            "post-published",
            FloorDate.AddDays(1),
            [new SocialPostMedia(
                SocialMediaKind.Image,
                new Uri("https://cdn.example.test/published.jpg"),
                null)]);
        var fixture = CreateFixture([post]);

        await fixture.Handler.Handle(
            new ImportSocialActualitiesCommand(),
            CancellationToken.None);

        var actuality = fixture.ImportedActualities.Should().ContainSingle().Which;
        actuality.Status.Should().Be(ActualityStatus.Published);
    }

    [Fact]
    public async Task Handle_DoesNotPersistAnImportedActualityWhenItsArticleIsEmpty()
    {
        var post = new SocialPost(
            "post-empty-caption",
            Caption: null,
            new Uri("https://www.instagram.com/p/post-empty-caption/"),
            FloorDate.AddDays(1),
            [new SocialPostMedia(
                SocialMediaKind.Image,
                new Uri("https://cdn.example.test/empty-caption.jpg"),
                null)]);
        var fixture = CreateFixture([post]);

        var result = await fixture.Handler.Handle(
            new ImportSocialActualitiesCommand(),
            CancellationToken.None);

        result.FailedCount.Should().Be(1);
        fixture.ImportedActualities.Should().BeEmpty();
        await fixture.Store.DidNotReceive().PersistAsync(
            Arg.Any<ActualityAggregate>(),
            Arg.Any<SocialPostImport>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_UsesFallbackTitleAndMarksItForReviewWhenGeneratorReturnsNull()
    {
        var post = CreatePost(
            "post-no-title",
            FloorDate.AddDays(2),
            [new SocialPostMedia(
                SocialMediaKind.Image,
                new Uri("https://cdn.example.test/no-title.jpg"),
                null)]);
        var fixture = CreateFixture([post]);
        fixture.TitleGenerator.GenerateAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<string?>(null));

        await fixture.Handler.Handle(new ImportSocialActualitiesCommand(), CancellationToken.None);

        var actuality = fixture.ImportedActualities.Should().ContainSingle().Which;
        actuality.Title.Should().Be("Actualité du 3 septembre 2026");
        actuality.TitleNeedsReview.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_DoesNotWriteAnythingWhenOneMediaDownloadFails()
    {
        var post = CreatePost(
            "post-broken-media",
            FloorDate.AddDays(1),
            [
                new SocialPostMedia(
                    SocialMediaKind.Image,
                    new Uri("https://cdn.example.test/ok.jpg"),
                    null),
                new SocialPostMedia(
                    SocialMediaKind.Image,
                    new Uri("https://cdn.example.test/broken.jpg"),
                    null),
            ]);
        var fixture = CreateFixture([post]);
        fixture.Downloader.DownloadAsync(
                new Uri("https://cdn.example.test/broken.jpg"),
                Arg.Any<CancellationToken>())
            .Returns(Task.FromException<Stream>(new HttpRequestException("cdn unavailable")));

        var result = await fixture.Handler.Handle(
            new ImportSocialActualitiesCommand(),
            CancellationToken.None);

        result.FailedCount.Should().Be(1);
        fixture.ImportedActualities.Should().BeEmpty();
        await fixture.Store.DidNotReceive().PersistAsync(
            Arg.Any<ActualityAggregate>(),
            Arg.Any<SocialPostImport>(),
            Arg.Any<CancellationToken>());
        await fixture.Blob.DidNotReceive().UploadActualityImagesAsync(
            Arg.Any<string>(),
            Arg.Any<Stream>());
    }

    [Fact]
    public async Task Handle_CleansUpUploadedMediaWhenALaterUploadFails()
    {
        var post = CreatePost(
            "post-broken-upload",
            FloorDate.AddDays(1),
            [
                new SocialPostMedia(
                    SocialMediaKind.Image,
                    new Uri("https://cdn.example.test/first.jpg"),
                    null),
                new SocialPostMedia(
                    SocialMediaKind.Image,
                    new Uri("https://cdn.example.test/second.jpg"),
                    null),
            ]);
        var fixture = CreateFixture([post]);
        fixture.Blob.UploadActualityImagesAsync(
                Arg.Is<string>(fileName => fileName.EndsWith("-1.jpg", StringComparison.Ordinal)),
                Arg.Any<Stream>())
            .Returns(Task.FromException<Uri>(new IOException("blob unavailable")));

        var result = await fixture.Handler.Handle(
            new ImportSocialActualitiesCommand(),
            CancellationToken.None);

        result.FailedCount.Should().Be(1);
        fixture.ImportedActualities.Should().BeEmpty();
        await fixture.Blob.Received(1).DeleteFileAsync(
            Arg.Is<string>(uri => uri.Contains("-0.jpg", StringComparison.Ordinal)));
        await fixture.Store.DidNotReceive().PersistAsync(
            Arg.Any<ActualityAggregate>(),
            Arg.Any<SocialPostImport>(),
            Arg.Any<CancellationToken>());
    }

    private static SocialPost CreatePost(
        string externalId,
        DateTimeOffset publishedAt,
        IReadOnlyList<SocialPostMedia> medias) =>
        new(
            externalId,
            "Une légende de publication\n\n#association",
            new Uri($"https://www.instagram.com/p/{externalId}/"),
            publishedAt,
            medias);

    private static Fixture CreateFixture(IReadOnlyList<SocialPost> posts)
    {
        var feed = Substitute.For<ISocialFeedClient>();
        feed.GetRecentPostsAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(posts));

        var store = Substitute.For<IActualityImportStore>();
        var knownExternalIds = new HashSet<string>(StringComparer.Ordinal);
        store.GetImportedExternalIdsAsync(
                SocialPostSource.Instagram,
                Arg.Any<CancellationToken>())
            .Returns(_ => Task.FromResult<IReadOnlySet<string>>(
                new HashSet<string>(knownExternalIds, StringComparer.Ordinal)));

        var importedActualities = new List<ActualityAggregate>();
        var importedExternalIds = new List<string>();
        store.PersistAsync(
                Arg.Do<ActualityAggregate>(importedActualities.Add),
                Arg.Do<SocialPostImport>(import =>
                {
                    importedExternalIds.Add(import.ExternalId);
                    knownExternalIds.Add(import.ExternalId);
                }),
                Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);

        var downloader = Substitute.For<IMediaDownloader>();
        downloader.DownloadAsync(Arg.Any<Uri>(), Arg.Any<CancellationToken>())
            .Returns(_ => Task.FromResult<Stream>(new MemoryStream([1, 2, 3])));

        var blob = Substitute.For<IBlobService>();
        blob.UploadActualityImagesAsync(Arg.Any<string>(), Arg.Any<Stream>())
            .Returns(call => Task.FromResult(
                new Uri($"https://blob.example.test/{call.Arg<string>()}")));

        var titleGenerator = Substitute.For<IActualityTitleGenerator>();
        titleGenerator.GenerateAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<string?>("Une actualité associative"));

        var clock = Substitute.For<IDateTimeProvider>();
        clock.UtcNow.Returns(ImportedAt.UtcDateTime);

        var options = Options.Create(new SocialImportOptions
        {
            ImportFloorDate = FloorDate,
            MaxPostsPerRun = 5,
        });
        var handler = new ImportSocialActualitiesCommandHandler(
            feed,
            store,
            downloader,
            blob,
            titleGenerator,
            clock,
            options,
            NullLogger<ImportSocialActualitiesCommandHandler>.Instance);

        return new Fixture(
            handler,
            store,
            downloader,
            blob,
            titleGenerator,
            importedActualities,
            importedExternalIds);
    }

    private sealed record Fixture(
        ImportSocialActualitiesCommandHandler Handler,
        IActualityImportStore Store,
        IMediaDownloader Downloader,
        IBlobService Blob,
        IActualityTitleGenerator TitleGenerator,
        List<ActualityAggregate> ImportedActualities,
        List<string> ImportedExternalIds);
}
