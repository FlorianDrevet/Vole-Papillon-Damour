using System.Text.Json;
using System.Text.Json.Serialization;
using Vole_Papillon_Damour.Domain.Common;
using Vole_Papillon_Damour.Domain.Common.Models;
using Vole_Papillon_Damour.Domain.BookAggregate.Entities;
using Vole_Papillon_Damour.Domain.BookAggregate.ValueObjects;

namespace Vole_Papillon_Damour.Domain.BookAggregate;

public sealed class Book : AggregateRoot<Isbn13>
{
    private readonly List<BookGenre> _genres = [];

    public Isbn13 Isbn13 => Id;
    public Isbn13? RedirectedToIsbn13 { get; private set; }
    public string? WorkId { get; private set; }
    public string? Title { get; private set; }
    public string? Authors { get; private set; }
    public string? Publisher { get; private set; }
    public int? PublicationYear { get; private set; }
    public string? PhysicalFormat { get; private set; }
    public string? Language { get; private set; }
    public string? Summary { get; private set; }
    public string? Genre { get; private set; }
    public IReadOnlyCollection<BookGenre> Genres => _genres;

    public int QuantityAvailable { get; private set; }
    public int SalesCount { get; private set; }
    public int RejectionCount { get; private set; }

    public bool IsHiddenFromCatalog { get; private set; }
    public string? CoverUrl { get; private set; }
    public BookCoverSource? CoverSource { get; private set; }
    public DateTime? CoverCheckedAt { get; private set; }

    public BookMetadataStatus MetadataStatus { get; private set; }
    public BookMetadataSource? MetadataSource { get; private set; }
    public DateTime? MetadataFetchedAt { get; private set; }
    public DateTime? GenresRefreshedAt { get; private set; }
    public DateTime? BibliographicDetailsRefreshedAt { get; private set; }
    public int ResolveAttempts { get; private set; }
    public DateTime? LastAttemptAt { get; private set; }
    public string? MetadataFailureReason { get; private set; }
    public string? RawPayload { get; private set; }
    public string? ManuallyEditedFields { get; private set; }

    public DateTime FirstSeenAt { get; private set; }
    public DateTime? LastAvailableAt { get; private set; }
    public DateTime UpdatedAt { get; private set; }
    public byte[] RowVersion { get; private set; } = [];

    private Book(Isbn13 isbn13, DateTime firstSeenAt) : base(isbn13)
    {
        var utcFirstSeenAt = DomainTime.RequireUtc(firstSeenAt, nameof(firstSeenAt));
        EnsureIsbn(isbn13);

        MetadataStatus = BookMetadataStatus.Pending;
        FirstSeenAt = utcFirstSeenAt;
        UpdatedAt = utcFirstSeenAt;
    }

    public static Book Create(Isbn13 isbn13, DateTime firstSeenAt)
    {
        return new Book(isbn13, firstSeenAt);
    }

    public Book()
    {
    }

    public void RedirectTo(Isbn13 canonicalIsbn13)
    {
        EnsureIsbn(canonicalIsbn13);

        if (canonicalIsbn13 == Id)
        {
            throw new ArgumentException("A book cannot redirect to itself.", nameof(canonicalIsbn13));
        }

        if (RedirectedToIsbn13 is not null)
        {
            throw new InvalidOperationException("A redirected book cannot be redirected again.");
        }

        RedirectedToIsbn13 = canonicalIsbn13;
        IsHiddenFromCatalog = true;
    }

    public void RecordAvailableEntry(DateTime occurredAt, int quantity = 1)
    {
        if (quantity <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(quantity), "Available entry quantity must be positive.");
        }

        var utcOccurredAt = DomainTime.RequireUtc(occurredAt, nameof(occurredAt));

        QuantityAvailable += quantity;
        LastAvailableAt = utcOccurredAt;
        UpdatedAt = utcOccurredAt;
    }

    public void RecordAnnouncementEntry(DateTime occurredAt)
    {
        UpdatedAt = DomainTime.RequireUtc(occurredAt, nameof(occurredAt));
    }

    public void RecordSale(DateTime occurredAt, int quantity = 1)
    {
        if (quantity <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(quantity), "Sale quantity must be positive.");
        }

        var utcOccurredAt = DomainTime.RequireUtc(occurredAt, nameof(occurredAt));

        QuantityAvailable = Math.Max(0, QuantityAvailable - quantity);
        SalesCount += quantity;
        UpdatedAt = utcOccurredAt;
    }

    public void ReverseSale(DateTime occurredAt, int quantity = 1)
    {
        if (quantity <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(quantity), "Sale reversal quantity must be positive.");
        }

        var utcOccurredAt = DomainTime.RequireUtc(occurredAt, nameof(occurredAt));

        QuantityAvailable += quantity;
        SalesCount = Math.Max(0, SalesCount - quantity);
        UpdatedAt = utcOccurredAt;
    }

    public int ApplyQuantityCorrection(int quantityAvailable, DateTime occurredAt)
    {
        if (quantityAvailable < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(quantityAvailable),
                "The corrected quantity cannot be negative.");
        }

        var utcOccurredAt = DomainTime.RequireUtc(occurredAt, nameof(occurredAt));
        var delta = quantityAvailable - QuantityAvailable;
        QuantityAvailable = quantityAvailable;
        UpdatedAt = utcOccurredAt;
        return delta;
    }

    public bool UpdateCatalogVisibility(bool isHidden, DateTime updatedAt)
    {
        var utcUpdatedAt = DomainTime.RequireUtc(updatedAt, nameof(updatedAt));
        var changed = IsHiddenFromCatalog != isHidden;
        IsHiddenFromCatalog = isHidden;
        UpdatedAt = utcUpdatedAt;
        return changed;
    }

    public bool ApplyManualMetadata(BookMetadataPatch patch, DateTime updatedAt)
    {
        ArgumentNullException.ThrowIfNull(patch);
        var utcUpdatedAt = DomainTime.RequireUtc(updatedAt, nameof(updatedAt));
        var fields = patch.Fields?.Distinct().ToArray()
            ?? throw new ArgumentNullException(nameof(patch.Fields));

        if (fields.Length == 0)
        {
            throw new ArgumentException(
                "At least one metadata field must be selected.",
                nameof(patch));
        }

        ValidatePatch(patch, fields);

        var manuallyEditedFields = ReadManuallyEditedFields();
        var changed = MetadataStatus != BookMetadataStatus.Manual ||
                      MetadataSource != BookMetadataSource.Manual;

        foreach (var field in fields)
        {
            if (!manuallyEditedFields.Contains(field))
            {
                manuallyEditedFields.Add(field);
                changed = true;
            }

            changed |= ApplyField(patch, field);

            if (field == BookMetadataField.CoverUrl)
            {
                changed |= CoverSource != BookCoverSource.Manual;
                CoverSource = BookCoverSource.Manual;
                changed |= CoverCheckedAt != utcUpdatedAt;
                CoverCheckedAt = utcUpdatedAt;
            }
        }

        MetadataStatus = BookMetadataStatus.Manual;
        MetadataSource = BookMetadataSource.Manual;
        MetadataFailureReason = null;
        ManuallyEditedFields = JsonSerializer.Serialize(
            manuallyEditedFields,
            MetadataJsonOptions);
        UpdatedAt = utcUpdatedAt;
        return changed;
    }

    public bool ApplyAutomaticMetadata(
        BookMetadataPatch patch,
        BookMetadataSource source,
        DateTime fetchedAt,
        string? rawPayload,
        BookCoverSource? coverSource = null,
        DateTime? coverCheckedAt = null)
    {
        ArgumentNullException.ThrowIfNull(patch);
        if (source is not (BookMetadataSource.Bnf or BookMetadataSource.OpenLibrary or BookMetadataSource.GoogleBooks))
        {
            throw new ArgumentException(
                "Automatic metadata must use a bibliographic source.",
                nameof(source));
        }

        var utcFetchedAt = DomainTime.RequireUtc(fetchedAt, nameof(fetchedAt));
        var fields = patch.Fields?.Distinct().ToArray()
            ?? throw new ArgumentNullException(nameof(patch.Fields));
        if (fields.Length == 0)
        {
            throw new ArgumentException(
                "At least one metadata field must be selected.",
                nameof(patch));
        }

        ValidatePatch(patch, fields);

        if (fields.Contains(BookMetadataField.CoverUrl) &&
            patch.CoverUrl is not null &&
            coverSource is null)
        {
            throw new ArgumentException(
                "An automatic cover URL must identify its provider.",
                nameof(coverSource));
        }

        var manuallyEditedFields = ReadManuallyEditedFields();
        var changed = false;
        foreach (var field in fields)
        {
            if (!manuallyEditedFields.Contains(field))
            {
                changed |= ApplyField(patch, field);

                if (field == BookMetadataField.CoverUrl)
                {
                    changed |= CoverSource != coverSource;
                    CoverSource = coverSource;
                }
            }
        }

        var coverCheckInstant = coverCheckedAt ??
                                 (fields.Contains(BookMetadataField.CoverUrl)
                                     ? utcFetchedAt
                                     : null);
        if (coverCheckInstant is { } checkedAt)
        {
            var utcCheckedAt = DomainTime.RequireUtc(
                checkedAt,
                nameof(coverCheckedAt));
            changed |= CoverCheckedAt != utcCheckedAt;
            CoverCheckedAt = utcCheckedAt;
        }

        if (ResolveAttempts < int.MaxValue)
        {
            ResolveAttempts++;
            changed = true;
        }
        var genreRefreshAt = fields.Contains(BookMetadataField.Genre) ? utcFetchedAt : GenresRefreshedAt;
        changed |= LastAttemptAt != utcFetchedAt ||
                   MetadataFetchedAt != utcFetchedAt ||
                   GenresRefreshedAt != genreRefreshAt ||
                   RawPayload != rawPayload;
        LastAttemptAt = utcFetchedAt;
        MetadataFetchedAt = utcFetchedAt;
        GenresRefreshedAt = genreRefreshAt;
        RawPayload = rawPayload;

        var nextStatus = manuallyEditedFields.Count == 0
            ? BookMetadataStatus.Resolved
            : BookMetadataStatus.Manual;
        var nextSource = manuallyEditedFields.Count == 0
            ? source
            : BookMetadataSource.Manual;
        changed |= MetadataStatus != nextStatus || MetadataSource != nextSource;
        MetadataStatus = nextStatus;
        MetadataSource = nextSource;
        MetadataFailureReason = null;
        UpdatedAt = utcFetchedAt;
        return changed;
    }

    public bool RecordCoverCheck(DateTime checkedAt)
    {
        var utcCheckedAt = DomainTime.RequireUtc(checkedAt, nameof(checkedAt));
        var changed = CoverCheckedAt != utcCheckedAt;
        CoverCheckedAt = utcCheckedAt;
        UpdatedAt = utcCheckedAt;
        return changed;
    }

    public bool RecordMetadataRefreshAttempt(DateTime attemptedAt)
    {
        var utcAttemptedAt = DomainTime.RequireUtc(attemptedAt, nameof(attemptedAt));
        if (MetadataStatus != BookMetadataStatus.Resolved)
        {
            return false;
        }

        var changed = LastAttemptAt != utcAttemptedAt;
        LastAttemptAt = utcAttemptedAt;
        UpdatedAt = utcAttemptedAt;
        return changed;
    }

    public bool RecordGenreRefreshAttempt(DateTime attemptedAt)
    {
        var utcAttemptedAt = DomainTime.RequireUtc(attemptedAt, nameof(attemptedAt));
        if (MetadataStatus != BookMetadataStatus.Resolved ||
            IsMetadataFieldManuallyEdited(BookMetadataField.Genre))
        {
            return false;
        }

        var changed = GenresRefreshedAt != utcAttemptedAt;
        GenresRefreshedAt = utcAttemptedAt;
        UpdatedAt = utcAttemptedAt;
        return changed;
    }

    public bool RecordBibliographicDetailsRefreshAttempt(DateTime attemptedAt)
    {
        var utcAttemptedAt = DomainTime.RequireUtc(attemptedAt, nameof(attemptedAt));
        if (MetadataStatus is not (BookMetadataStatus.Resolved or BookMetadataStatus.Manual))
        {
            return false;
        }

        var changed = BibliographicDetailsRefreshedAt != utcAttemptedAt;
        BibliographicDetailsRefreshedAt = utcAttemptedAt;
        UpdatedAt = utcAttemptedAt;
        return changed;
    }

    public bool IsMetadataFieldManuallyEdited(BookMetadataField field)
    {
        return ReadManuallyEditedFields().Contains(field);
    }

    public bool RecordMetadataNotFound(
        DateTime attemptedAt,
        string failureReason = "Aucune notice bibliographique trouvée pour cet ISBN.")
    {
        var utcAttemptedAt = DomainTime.RequireUtc(attemptedAt, nameof(attemptedAt));
        if (MetadataStatus == BookMetadataStatus.Discarded)
        {
            return false;
        }
        var normalizedReason = NormalizeFailureReason(failureReason);
        var changed = false;

        if (ResolveAttempts < int.MaxValue)
        {
            ResolveAttempts++;
            changed = true;
        }

        changed |= LastAttemptAt != utcAttemptedAt;
        LastAttemptAt = utcAttemptedAt;

        if (MetadataStatus != BookMetadataStatus.Manual)
        {
            changed |= MetadataStatus != BookMetadataStatus.NotFound ||
                       MetadataSource is not null ||
                       MetadataFetchedAt is not null ||
                       RawPayload is not null ||
                       MetadataFailureReason != normalizedReason;
            MetadataStatus = BookMetadataStatus.NotFound;
            MetadataSource = null;
            MetadataFetchedAt = null;
            RawPayload = null;
            MetadataFailureReason = normalizedReason;
        }

        UpdatedAt = utcAttemptedAt;
        return changed;
    }

    public bool RecordMetadataProviderFailure(
        DateTime attemptedAt,
        string failureReason = "Erreur lors de la consultation des fournisseurs de notices.",
        bool countAttempt = false)
    {
        var utcAttemptedAt = DomainTime.RequireUtc(attemptedAt, nameof(attemptedAt));
        if (MetadataStatus is BookMetadataStatus.Manual or BookMetadataStatus.Discarded)
        {
            return false;
        }

        var normalizedReason = NormalizeFailureReason(failureReason);
        var changed = LastAttemptAt != utcAttemptedAt ||
                      MetadataStatus != BookMetadataStatus.Failed ||
                      MetadataFailureReason != normalizedReason;
        if (countAttempt && ResolveAttempts < int.MaxValue)
        {
            ResolveAttempts++;
            changed = true;
        }

        MetadataStatus = BookMetadataStatus.Failed;
        MetadataFailureReason = normalizedReason;
        LastAttemptAt = utcAttemptedAt;
        UpdatedAt = utcAttemptedAt;
        return changed;
    }

    public bool PrepareMetadataRetry(DateTime retriedAt)
    {
        var utcRetriedAt = DomainTime.RequireUtc(retriedAt, nameof(retriedAt));
        if (MetadataStatus is BookMetadataStatus.Manual or BookMetadataStatus.Discarded)
        {
            return false;
        }

        var changed = MetadataStatus != BookMetadataStatus.Pending ||
                      LastAttemptAt is not null ||
                      MetadataFailureReason is not null;
        MetadataStatus = BookMetadataStatus.Pending;
        LastAttemptAt = null;
        MetadataFailureReason = null;
        UpdatedAt = utcRetriedAt;
        return changed;
    }

    public bool DiscardMetadataCandidate(DateTime discardedAt)
    {
        var utcDiscardedAt = DomainTime.RequireUtc(discardedAt, nameof(discardedAt));
        if (MetadataStatus == BookMetadataStatus.Discarded)
        {
            return false;
        }

        if ((MetadataStatus is BookMetadataStatus.Resolved or BookMetadataStatus.Manual) &&
            !string.IsNullOrWhiteSpace(Title))
        {
            throw new InvalidOperationException("A completed book cannot be discarded as an ISBN metadata candidate.");
        }

        var changed = MetadataStatus != BookMetadataStatus.Discarded ||
                      !IsHiddenFromCatalog ||
                      MetadataFailureReason is not null;
        MetadataStatus = BookMetadataStatus.Discarded;
        MetadataFailureReason = null;
        IsHiddenFromCatalog = true;
        UpdatedAt = utcDiscardedAt;
        return changed;
    }

    public void RecordRejection(DateTime occurredAt)
    {
        var utcOccurredAt = DomainTime.RequireUtc(occurredAt, nameof(occurredAt));

        RejectionCount++;
        UpdatedAt = utcOccurredAt;
    }

    private static void EnsureIsbn(Isbn13 isbn13)
    {
        if (string.IsNullOrWhiteSpace(isbn13.Value))
        {
            throw new ArgumentException("A valid ISBN-13 is required.", nameof(isbn13));
        }
    }

    private static void ValidatePatch(
        BookMetadataPatch patch,
        IEnumerable<BookMetadataField> fields)
    {
        foreach (var field in fields)
        {
            switch (field)
            {
                case BookMetadataField.Title:
                    ValidateLength(patch.Title, 500, nameof(patch.Title));
                    break;
                case BookMetadataField.Authors:
                    ValidateLength(patch.Authors, 500, nameof(patch.Authors));
                    break;
                case BookMetadataField.WorkId:
                    ValidateLength(patch.WorkId, 64, nameof(patch.WorkId));
                    break;
                case BookMetadataField.Publisher:
                    ValidateLength(patch.Publisher, 200, nameof(patch.Publisher));
                    break;
                case BookMetadataField.PublicationYear:
                    if (patch.PublicationYear is < 1 or > 9999)
                    {
                        throw new ArgumentOutOfRangeException(
                            nameof(patch.PublicationYear),
                            "The publication year must be between 1 and 9999.");
                    }

                    break;
                case BookMetadataField.PhysicalFormat:
                    ValidateLength(patch.PhysicalFormat, 50, nameof(patch.PhysicalFormat));
                    break;
                case BookMetadataField.Language:
                    ValidateLength(patch.Language, 10, nameof(patch.Language));
                    break;
                case BookMetadataField.Summary:
                    ValidateLength(patch.Summary, 20_000, nameof(patch.Summary));
                    break;
                case BookMetadataField.Genre:
                    ValidateLength(patch.Genre, 100, nameof(patch.Genre));
                    if (patch.Genres is not null)
                    {
                        foreach (var genre in patch.Genres)
                        {
                            ValidateLength(genre, 100, nameof(patch.Genres));
                        }
                    }
                    break;
                case BookMetadataField.CoverUrl:
                    ValidateCoverUrl(patch.CoverUrl);
                    break;
                default:
                    throw new ArgumentOutOfRangeException(
                        nameof(patch.Fields),
                        field,
                        "The metadata field is not supported.");
            }
        }
    }

    private static void ValidateLength(string? value, int maxLength, string parameterName)
    {
        if (value is not null && value.Length > maxLength)
        {
            throw new ArgumentException(
                $"The metadata field cannot exceed {maxLength} characters.",
                parameterName);
        }
    }

    private static string NormalizeFailureReason(string failureReason)
    {
        if (string.IsNullOrWhiteSpace(failureReason))
        {
            throw new ArgumentException("A metadata failure reason is required.", nameof(failureReason));
        }

        var normalized = failureReason.Trim();
        return normalized.Length <= 500 ? normalized : normalized[..500];
    }

    private bool ApplyField(BookMetadataPatch patch, BookMetadataField field)
    {
        switch (field)
        {
            case BookMetadataField.Title:
                return SetValue(Title, patch.Title, value => Title = value);
            case BookMetadataField.Authors:
                return SetValue(Authors, patch.Authors, value => Authors = value);
            case BookMetadataField.WorkId:
                return SetValue(WorkId, patch.WorkId, value => WorkId = value);
            case BookMetadataField.Publisher:
                return SetValue(Publisher, patch.Publisher, value => Publisher = value);
            case BookMetadataField.PublicationYear:
                return SetValue(PublicationYear, patch.PublicationYear, value => PublicationYear = value);
            case BookMetadataField.PhysicalFormat:
                return SetValue(PhysicalFormat, patch.PhysicalFormat, value => PhysicalFormat = value);
            case BookMetadataField.Language:
                return SetValue(Language, patch.Language, value => Language = value);
            case BookMetadataField.Summary:
                return SetValue(Summary, patch.Summary, value => Summary = value);
            case BookMetadataField.Genre:
                var primaryGenreChanged = SetValue(Genre, patch.Genre, value => Genre = value);
                var genreNames = patch.Genres ?? (patch.Genre is null ? [] : [patch.Genre]);
                return ReplaceGenres(genreNames) || primaryGenreChanged;
            case BookMetadataField.CoverUrl:
                return SetValue(CoverUrl, patch.CoverUrl, value => CoverUrl = value);
            default:
                throw new ArgumentOutOfRangeException(nameof(field), field, "The metadata field is not supported.");
        }
    }

    private bool ReplaceGenres(IEnumerable<string> genreNames)
    {
        var normalized = new List<string>();
        foreach (var name in genreNames)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                continue;
            }

            var normalizedName = string.Join(' ', name.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
            if (normalizedName.Length <= 100 &&
                !normalized.Any(existing => BookGenre.NamesAreEquivalent(existing, normalizedName)))
            {
                normalized.Add(normalizedName);
            }
        }

        var removedGenres = _genres
            .Where(existing => !normalized.Any(name => BookGenre.NamesAreEquivalent(existing.Name, name)))
            .ToArray();
        var addedNames = normalized
            .Where(name => !_genres.Any(existing => BookGenre.NamesAreEquivalent(existing.Name, name)))
            .ToArray();
        if (removedGenres.Length == 0 && addedNames.Length == 0)
        {
            return false;
        }

        foreach (var removedGenre in removedGenres)
        {
            _genres.Remove(removedGenre);
        }

        _genres.AddRange(addedNames.Select(name => BookGenre.Create(Id, name)));
        return true;
    }

    private static bool SetValue<T>(T current, T value, Action<T> assign)
    {
        var changed = !EqualityComparer<T>.Default.Equals(current, value);
        assign(value);
        return changed;
    }

    private List<BookMetadataField> ReadManuallyEditedFields()
    {
        if (string.IsNullOrWhiteSpace(ManuallyEditedFields))
        {
            return [];
        }

        try
        {
            var normalizedFields = ManuallyEditedFields.Replace(
                "\"CoverBlobRef\"",
                "\"CoverUrl\"",
                StringComparison.Ordinal);
            return JsonSerializer.Deserialize<List<BookMetadataField>>(
                       normalizedFields,
                       MetadataJsonOptions)
                   ?.Distinct()
                   .ToList()
                   ?? [];
        }
        catch (JsonException)
        {
            return [];
        }
    }

    private static readonly JsonSerializerOptions MetadataJsonOptions = new()
    {
        Converters = { new JsonStringEnumConverter() }
    };

    private static void ValidateCoverUrl(string? value)
    {
        ValidateLength(value, 2048, nameof(BookMetadataPatch.CoverUrl));
        if (value is null)
        {
            return;
        }

        if (!Uri.TryCreate(value, UriKind.Absolute, out var uri) ||
            !string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException(
                "The cover URL must be an absolute HTTPS URL.",
                nameof(BookMetadataPatch.CoverUrl));
        }
    }
}
