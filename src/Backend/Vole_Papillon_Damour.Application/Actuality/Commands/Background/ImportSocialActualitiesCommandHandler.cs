using System.Security.Cryptography;
using MediatR;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Vole_Papillon_Damour.Application.Actuality.Common;
using Vole_Papillon_Damour.Application.Common.Interfaces.Persistence;
using Vole_Papillon_Damour.Application.Common.Interfaces.Services;
using Vole_Papillon_Damour.Application.Common.Models;
using Vole_Papillon_Damour.Domain.ActualityAggregate.ValueObjects;
using ActualityAggregate = Vole_Papillon_Damour.Domain.ActualityAggregate.Actuality;
using SocialPostImportAggregate = Vole_Papillon_Damour.Domain.ActualityAggregate.SocialPostImport;

namespace Vole_Papillon_Damour.Application.Actuality.Commands.Background;

public sealed class ImportSocialActualitiesCommandHandler(
    ISocialFeedClient feedClient,
    IActualityImportStore importStore,
    IMediaDownloader mediaDownloader,
    IBlobService blobService,
    IActualityTitleGenerator titleGenerator,
    IDateTimeProvider dateTimeProvider,
    IOptions<SocialImportOptions> options,
    ILogger<ImportSocialActualitiesCommandHandler> logger)
    : IRequestHandler<ImportSocialActualitiesCommand, ImportSocialActualitiesResult>
{
    private const SocialPostSource Source = SocialPostSource.Instagram;
    private const int MaximumMediaPerActuality = 10;
    private readonly SocialImportOptions _options = options.Value;

    public async Task<ImportSocialActualitiesResult> Handle(
        ImportSocialActualitiesCommand command,
        CancellationToken cancellationToken)
    {
        if (_options.MaxPostsPerRun <= 0)
        {
            throw new InvalidOperationException("SocialImport:MaxPostsPerRun must be positive.");
        }

        var posts = await feedClient.GetRecentPostsAsync(cancellationToken);
        var knownExternalIds = await importStore.GetImportedExternalIdsAsync(Source, cancellationToken);
        var alreadyKnownCount = posts.Count(post => knownExternalIds.Contains(post.ExternalId));
        var candidates = posts
            .Where(post => post.PublishedAt >= _options.ImportFloorDate &&
                          !knownExternalIds.Contains(post.ExternalId))
            .GroupBy(post => post.ExternalId, StringComparer.Ordinal)
            .Select(group => group.First())
            .OrderBy(post => post.PublishedAt)
            .Take(_options.MaxPostsPerRun)
            .ToList();

        var importedCount = 0;
        var failedCount = 0;
        var noMediaCount = 0;
        var fallbackTitleCount = 0;

        foreach (var post in candidates)
        {
            try
            {
                var imported = await ImportPostAsync(
                    post,
                    cancellationToken);
                if (!imported)
                {
                    noMediaCount++;
                    continue;
                }

                importedCount++;
                if (imported.TitleUsedFallback)
                {
                    fallbackTitleCount++;
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (NoUsableMediaException)
            {
                noMediaCount++;
                logger.LogInformation(
                    "Social actuality skipped because it has no usable media. ExternalId: {ExternalId}",
                    post.ExternalId);
            }
            catch (MediaDownloadException exception)
            {
                failedCount++;
                logger.LogWarning(
                    exception,
                    "Social actuality import failed while downloading media. ExternalId: {ExternalId}",
                    post.ExternalId);
            }
            catch (Exception exception)
            {
                failedCount++;
                logger.LogWarning(
                    exception,
                    "Social actuality import failed. ExternalId: {ExternalId}",
                    post.ExternalId);
            }
        }

        return new ImportSocialActualitiesResult(
            posts.Count,
            importedCount,
            alreadyKnownCount,
            failedCount,
            noMediaCount,
            fallbackTitleCount);
    }

    private async Task<ImportedPost> ImportPostAsync(
        SocialPost post,
        CancellationToken cancellationToken)
    {
        if (post.Medias.Count > MaximumMediaPerActuality)
        {
            logger.LogInformation(
                "Social actuality contains more than {MaximumMedia} media; excess media are ignored. " +
                "ExternalId: {ExternalId}",
                MaximumMediaPerActuality,
                post.ExternalId);
        }

        var media = post.Medias
            .Select(GetDownloadUri)
            .Where(media => media is not null)
            .Select(media => media!)
            .Take(MaximumMediaPerActuality)
            .ToList();
        if (media.Count == 0)
        {
            throw new NoUsableMediaException();
        }

        var downloadedMedia = new List<DownloadedMedia>(media.Count);
        var uploadedMedia = new List<Uri>(media.Count);
        try
        {
            foreach (var mediaUri in media)
            {
                try
                {
                    await using var remoteStream = await mediaDownloader.DownloadAsync(
                        mediaUri,
                        cancellationToken);
                    var content = new MemoryStream();
                    await remoteStream.CopyToAsync(content, cancellationToken);
                    content.Position = 0;
                    downloadedMedia.Add(new DownloadedMedia(content, GetExtension(mediaUri)));
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    throw;
                }
                catch (Exception exception)
                {
                    throw new MediaDownloadException(exception);
                }
            }

            var imageUris = new List<Uri>(downloadedMedia.Count);
            for (var index = 0; index < downloadedMedia.Count; index++)
            {
                var downloaded = downloadedMedia[index];
                downloaded.Stream.Position = 0;
                var fileName = CreateFileName(post.ExternalId, index, downloaded.Extension);
                var imageUri = await blobService.UploadActualityImagesAsync(
                    fileName,
                    downloaded.Stream);
                uploadedMedia.Add(imageUri);
                imageUris.Add(imageUri);
            }

            var article = CaptionCleaner.Clean(post.Caption);
            var title = await GenerateTitleOrFallbackAsync(
                article,
                post.PublishedAt,
                cancellationToken);
            var importedAt = new DateTimeOffset(dateTimeProvider.UtcNow, TimeSpan.Zero);
            var actuality = ActualityAggregate.CreateImported(
                title.Title,
                article,
                imageUris[0],
                post.Permalink,
                imageUris.Skip(1).ToList(),
                post.PublishedAt,
                importedAt,
                title.UsedFallback);
            if (!actuality.Publish())
            {
                throw new InvalidOperationException(
                    "The imported actuality cannot be published because its title or article is empty.");
            }

            var import = SocialPostImportAggregate.Create(
                Guid.NewGuid(),
                Source,
                post.ExternalId,
                post.Permalink,
                post.PublishedAt,
                importedAt,
                actuality.Id);

            await importStore.PersistAsync(actuality, import, cancellationToken);
            return new ImportedPost(true, title.UsedFallback);
        }
        catch
        {
            await CleanupUploadedMediaAsync(uploadedMedia, post.ExternalId);
            throw;
        }
        finally
        {
            foreach (var downloaded in downloadedMedia)
            {
                await downloaded.Stream.DisposeAsync();
            }
        }
    }

    private async Task CleanupUploadedMediaAsync(
        IEnumerable<Uri> uploadedMedia,
        string externalId)
    {
        foreach (var imageUri in uploadedMedia)
        {
            try
            {
                await blobService.DeleteFileAsync(imageUri.ToString());
            }
            catch (Exception exception)
            {
                logger.LogWarning(
                    exception,
                    "Social actuality media cleanup failed after an import error. ExternalId: {ExternalId}",
                    externalId);
            }
        }
    }

    private async Task<GeneratedTitle> GenerateTitleOrFallbackAsync(
        string article,
        DateTimeOffset publicationDate,
        CancellationToken cancellationToken)
    {
        string? generatedTitle;
        try
        {
            generatedTitle = await titleGenerator.GenerateAsync(article, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch
        {
            generatedTitle = null;
        }

        if (!string.IsNullOrWhiteSpace(generatedTitle))
        {
            return new GeneratedTitle(generatedTitle.Trim(), UsedFallback: false);
        }

        return new GeneratedTitle(
            FallbackTitle.Create(publicationDate),
            UsedFallback: true);
    }

    private static Uri? GetDownloadUri(SocialPostMedia media)
    {
        return media.Kind == SocialMediaKind.Video
            ? media.ThumbnailUrl
            : media.ContentUrl;
    }

    private static string GetExtension(Uri uri)
    {
        var extension = Path.GetExtension(uri.AbsolutePath);
        return string.IsNullOrWhiteSpace(extension) || extension.Length > 10
            ? ".jpg"
            : extension.ToLowerInvariant();
    }

    private static string CreateFileName(string externalId, int index, string extension)
    {
        var hash = Convert.ToHexString(SHA256.HashData(
            System.Text.Encoding.UTF8.GetBytes(externalId)))[..16].ToLowerInvariant();
        return $"social/{hash}-{index}{extension}";
    }

    private sealed record DownloadedMedia(MemoryStream Stream, string Extension);

    private sealed record GeneratedTitle(string Title, bool UsedFallback);

    private sealed record ImportedPost(bool Succeeded, bool TitleUsedFallback)
    {
        public static implicit operator bool(ImportedPost result) => result.Succeeded;
    }

    private sealed class MediaDownloadException(Exception innerException)
        : Exception("A social actuality media download failed.", innerException);

    private sealed class NoUsableMediaException : Exception;
}
