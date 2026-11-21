using Vole_Papillon_Damour.Domain.BookAggregate.ValueObjects;
using Vole_Papillon_Damour.Domain.Common;
using Vole_Papillon_Damour.Domain.UserAggregate.ValueObjects;

namespace Vole_Papillon_Damour.Domain.BookAggregate.Entities;

// Photo d'un livre sans ISBN (Q11). Le fichier est stocké par le service de blob ; la fiche garde son adresse.
public sealed class BookPhoto
{
    public const int MaximumPerBook = 10;
    public const long MaximumSizeBytes = 8 * 1024 * 1024;

    private static readonly string[] SupportedContentTypes = ["image/jpeg", "image/png", "image/webp"];

    public Guid Id { get; private set; }
    public BookCode BookIsbn13 { get; private set; }
    public Uri BlobUri { get; private set; } = null!;
    public string BlobName { get; private set; } = null!;
    public int Position { get; private set; }
    public string ContentType { get; private set; } = null!;
    public long SizeBytes { get; private set; }
    public DateTime UploadedAt { get; private set; }
    public UserId UploadedBy { get; private set; } = null!;

    private BookPhoto()
    {
    }

    public static BookPhoto Create(
        Guid id,
        BookCode bookCode,
        Uri blobUri,
        string blobName,
        int position,
        string contentType,
        long sizeBytes,
        DateTime uploadedAt,
        UserId uploadedBy)
    {
        if (id == Guid.Empty)
        {
            throw new ArgumentException("A book photo identifier is required.", nameof(id));
        }

        ArgumentNullException.ThrowIfNull(blobUri);
        if (!blobUri.IsAbsoluteUri)
        {
            throw new ArgumentException("A book photo needs an absolute blob address.", nameof(blobUri));
        }

        if (string.IsNullOrWhiteSpace(blobName))
        {
            throw new ArgumentException("A blob name is required.", nameof(blobName));
        }

        if (position < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(position), "A photo position cannot be negative.");
        }

        var normalizedType = contentType?.Trim().ToLowerInvariant() ?? string.Empty;
        if (!SupportedContentTypes.Contains(normalizedType))
        {
            throw new ArgumentException("Unsupported photo content type.", nameof(contentType));
        }

        if (sizeBytes is <= 0 or > MaximumSizeBytes)
        {
            throw new ArgumentOutOfRangeException(nameof(sizeBytes), "A photo size must be between 1 byte and 8 MB.");
        }

        ArgumentNullException.ThrowIfNull(uploadedBy);

        return new BookPhoto
        {
            Id = id,
            BookIsbn13 = bookCode,
            BlobUri = blobUri,
            BlobName = blobName.Trim(),
            Position = position,
            ContentType = normalizedType,
            SizeBytes = sizeBytes,
            UploadedAt = DomainTime.RequireUtc(uploadedAt, nameof(uploadedAt)),
            UploadedBy = uploadedBy,
        };
    }
}