using ErrorOr;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Vole_Papillon_Damour.Application.Common.Interfaces.Persistence;
using Vole_Papillon_Damour.Application.Common.Interfaces.Services;
using Vole_Papillon_Damour.Domain.BookAggregate.ValueObjects;
using Vole_Papillon_Damour.Domain.Common.Errors;

namespace Vole_Papillon_Damour.Application.Books.Commands.UploadBookCover;

public sealed class UploadBookCoverCommandHandler(
    IProjectDbContext dbContext,
    IBlobService blobService,
    IDateTimeProvider dateTimeProvider,
    ILogger<UploadBookCoverCommandHandler> logger)
    : IRequestHandler<UploadBookCoverCommand, ErrorOr<UploadBookCoverResult>>
{
    private const long MaxCoverSizeBytes = 8 * 1024 * 1024;

    public async Task<ErrorOr<UploadBookCoverResult>> Handle(
        UploadBookCoverCommand command,
        CancellationToken cancellationToken)
    {
        if (!Isbn13.TryCreate(command.Isbn13, out var requestedIsbn13))
        {
            return Errors.Book.InvalidIsbn(command.Isbn13);
        }

        var imageType = GetImageType(command.ContentType);
        if (imageType is null)
        {
            return Error.Validation(
                "Book.InvalidCoverType",
                "Book covers must be JPEG, PNG, or WebP images.");
        }

        if (command.SizeBytes <= 0 || command.SizeBytes > MaxCoverSizeBytes)
        {
            return Error.Validation(
                "Book.InvalidCoverSize",
                $"Book covers must be between 1 byte and {MaxCoverSizeBytes} bytes.");
        }

        if (command.Stream is null)
        {
            return Error.Validation("Book.InvalidCoverData", "A cover image file is required.");
        }

        if (command.UpdatedBy is null || command.UpdatedBy.Value == Guid.Empty)
        {
            return Error.Validation("Book.InvalidUpdatedBy", "An updating user identifier is required.");
        }

        var updatedAt = dateTimeProvider.UtcNow;
        if (updatedAt.Kind != DateTimeKind.Utc)
        {
            return Errors.Book.InvalidScanTimestamp();
        }

        var book = await dbContext.Books
            .SingleOrDefaultAsync(candidate => candidate.Id == requestedIsbn13, cancellationToken);
        if (book?.RedirectedToIsbn13 is { } canonicalIsbn13)
        {
            book = await dbContext.Books
                .SingleOrDefaultAsync(candidate => candidate.Id == canonicalIsbn13, cancellationToken);
            if (book is null)
            {
                return Errors.Book.RedirectTargetNotFound(canonicalIsbn13.Value);
            }
        }

        if (book is null)
        {
            return Errors.Book.NotFound(requestedIsbn13.Value);
        }

        var previousCoverUrl = book.CoverUrl;
        var blobName = $"{book.Isbn13.Value}/{Guid.NewGuid():N}{imageType.Value.Extension}";
        var blobUri = await blobService.UploadBookCoverAsync(
            blobName,
            command.Stream,
            imageType.Value.ContentType,
            cancellationToken);

        bool changed;
        try
        {
            await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
            changed = book.ApplyManualMetadata(
                new BookMetadataPatch(
                    null,
                    null,
                    null,
                    null,
                    null,
                    null,
                    null,
                    blobUri.ToString(),
                    [BookMetadataField.CoverUrl]),
                updatedAt);

            await dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch (Exception exception)
        {
            await TryDeleteBlobAsync(blobUri.ToString(), book.Isbn13.Value, "failed cover update");
            logger.LogWarning(
                exception,
                "Could not save the uploaded cover for book {Isbn13}; the new blob was removed when possible.",
                book.Isbn13.Value);
            throw;
        }

        if (!string.IsNullOrWhiteSpace(previousCoverUrl) &&
            !string.Equals(previousCoverUrl, blobUri.ToString(), StringComparison.Ordinal))
        {
            await TryDeleteBlobAsync(previousCoverUrl, book.Isbn13.Value, "replaced cover");
        }

        return new UploadBookCoverResult(
            book.Isbn13.Value,
            blobUri,
            book.MetadataStatus,
            book.ManuallyEditedFields,
            book.UpdatedAt,
            changed);
    }

    private async Task TryDeleteBlobAsync(string url, string isbn13, string reason)
    {
        try
        {
            await blobService.DeleteFileAsync(BlobContainer.BookCovers, url);
        }
        catch (Exception exception)
        {
            logger.LogWarning(
                exception,
                "Could not delete book-cover blob for {Isbn13} after {Reason}.",
                isbn13,
                reason);
        }
    }

    private static (string ContentType, string Extension)? GetImageType(string? contentType)
    {
        return contentType?.Trim().ToLowerInvariant() switch
        {
            "image/jpeg" => ("image/jpeg", ".jpg"),
            "image/png" => ("image/png", ".png"),
            "image/webp" => ("image/webp", ".webp"),
            _ => null
        };
    }
}
