using Vole_Papillon_Damour.Domain.Common;
using Vole_Papillon_Damour.Domain.Common.Models;
using Vole_Papillon_Damour.Domain.RareBookAggregate.ValueObjects;
using Vole_Papillon_Damour.Domain.UserAggregate.ValueObjects;

namespace Vole_Papillon_Damour.Domain.RareBookAggregate.Entities;

public sealed class RareBookPhoto : Entity<RareBookPhotoId>
{
    public RareBookId RareBookId { get; private set; } = null!;
    public Uri BlobUri { get; private set; } = null!;
    public string BlobName { get; private set; } = null!;
    public string? Caption { get; private set; }
    public int Position { get; private set; }
    public string ContentType { get; private set; } = null!;
    public long SizeBytes { get; private set; }
    public DateTime UploadedAt { get; private set; }
    public UserId UploadedBy { get; private set; } = null!;

    public RareBookPhoto()
    {
    }

    private RareBookPhoto(
        RareBookPhotoId id,
        RareBookId rareBookId,
        Uri blobUri,
        string blobName,
        string? caption,
        int position,
        string contentType,
        long sizeBytes,
        DateTime uploadedAt,
        UserId uploadedBy) : base(id)
    {
        if (id is null || id.Value == Guid.Empty)
        {
            throw new ArgumentException("A rare book photo identifier is required.", nameof(id));
        }

        RareBookId = EnsureRareBookId(rareBookId);
        BlobUri = EnsureBlobUri(blobUri);
        BlobName = NormalizeRequired(blobName, 1024, nameof(blobName));
        Caption = NormalizeOptional(caption, 80, nameof(caption));
        Position = position >= 0
            ? position
            : throw new ArgumentOutOfRangeException(nameof(position), "A photo position cannot be negative.");
        ContentType = NormalizeContentType(contentType);
        SizeBytes = sizeBytes > 0
            ? sizeBytes
            : throw new ArgumentOutOfRangeException(nameof(sizeBytes), "A photo size must be positive.");
        UploadedAt = DomainTime.RequireUtc(uploadedAt, nameof(uploadedAt));
        UploadedBy = EnsureUserId(uploadedBy);
    }

    public static RareBookPhoto Create(
        RareBookId rareBookId,
        Uri blobUri,
        string blobName,
        string? caption,
        int position,
        string contentType,
        long sizeBytes,
        DateTime uploadedAt,
        UserId uploadedBy)
    {
        return new RareBookPhoto(
            RareBookPhotoId.CreateUnique(),
            rareBookId,
            blobUri,
            blobName,
            caption,
            position,
            contentType,
            sizeBytes,
            uploadedAt,
            uploadedBy);
    }

    public static RareBookPhoto CreateWithId(
        RareBookPhotoId id,
        RareBookId rareBookId,
        Uri blobUri,
        string blobName,
        string? caption,
        int position,
        string contentType,
        long sizeBytes,
        DateTime uploadedAt,
        UserId uploadedBy)
    {
        return new RareBookPhoto(
            id,
            rareBookId,
            blobUri,
            blobName,
            caption,
            position,
            contentType,
            sizeBytes,
            uploadedAt,
            uploadedBy);
    }

    public static RareBookPhoto Create(
        RareBookId rareBookId,
        Uri blobUri,
        string blobName,
        string contentType,
        long sizeBytes,
        DateTime uploadedAt,
        UserId uploadedBy,
        string? caption = null,
        int position = 0)
    {
        return new RareBookPhoto(
            RareBookPhotoId.CreateUnique(),
            rareBookId,
            blobUri,
            blobName,
            caption,
            position,
            contentType,
            sizeBytes,
            uploadedAt,
            uploadedBy);
    }

    public bool UpdateCaption(string? caption)
    {
        var normalizedCaption = NormalizeOptional(caption, 80, nameof(caption));
        var changed = Caption != normalizedCaption;
        Caption = normalizedCaption;
        return changed;
    }

    internal void SetPosition(int position)
    {
        if (position < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(position));
        }

        Position = position;
    }

    private static RareBookId EnsureRareBookId(RareBookId? rareBookId)
    {
        if (rareBookId is null || rareBookId.Value == Guid.Empty)
        {
            throw new ArgumentException("A valid rare book identifier is required.", nameof(rareBookId));
        }

        return rareBookId;
    }

    private static UserId EnsureUserId(UserId? userId)
    {
        if (userId is null || userId.Value == Guid.Empty)
        {
            throw new ArgumentException("A valid user identifier is required.", nameof(userId));
        }

        return userId;
    }

    private static Uri EnsureBlobUri(Uri? blobUri)
    {
        if (blobUri is null ||
            !blobUri.IsAbsoluteUri ||
            !string.Equals(blobUri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException("A photo blob URI must be an absolute HTTPS URI.", nameof(blobUri));
        }

        return blobUri;
    }

    private static string NormalizeContentType(string value)
    {
        var normalized = NormalizeRequired(value, 40, nameof(value)).ToLowerInvariant();
        if (normalized is not "image/jpeg" and not "image/webp" and not "image/png")
        {
            throw new ArgumentException(
                "Rare book photos must use image/jpeg, image/webp, or image/png.",
                nameof(value));
        }

        return normalized;
    }

    private static string NormalizeRequired(string value, int maxLength, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("A value is required.", parameterName);
        }

        var normalized = value.Trim();
        if (normalized.Length > maxLength)
        {
            throw new ArgumentException(
                $"The value cannot exceed {maxLength} characters.",
                parameterName);
        }

        return normalized;
    }

    private static string? NormalizeOptional(string? value, int maxLength, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var normalized = value.Trim();
        if (normalized.Length > maxLength)
        {
            throw new ArgumentException(
                $"The value cannot exceed {maxLength} characters.",
                parameterName);
        }

        return normalized;
    }
}
