using Vole_Papillon_Damour.Domain.Common;
using Vole_Papillon_Damour.Domain.Common.Models;
using Vole_Papillon_Damour.Domain.RecommendationAggregate.ValueObjects;

namespace Vole_Papillon_Damour.Domain.RecommendationAggregate;

/// <summary>Profil de similarité d'une édition : notice lue aux sources, texte vectorisé, vecteur.</summary>
public sealed class BookSimilarityProfile : Entity<string>
{
    public const int Dimensions = 512;
    public const int EmbeddingBytes = Dimensions * sizeof(float);

    public string Isbn13
    {
        get => Id;
        private set => Id = value;
    }
    public string? NoticeJson { get; private set; }
    public bool NoticeFound { get; private set; }
    public DateTime? NoticeFetchedAt { get; private set; }
    public string? ProfileText { get; private set; }
    public string? ProfileTextHash { get; private set; }
    public byte[]? Embedding { get; private set; }
    public string? EmbeddedTextHash { get; private set; }
    public DateTime? EmbeddedAt { get; private set; }

    public bool NeedsEmbedding => ProfileTextHash is not null && ProfileTextHash != EmbeddedTextHash;

    private BookSimilarityProfile(string isbn13) : base(isbn13)
    {
    }

    private BookSimilarityProfile()
    {
    }

    public static BookSimilarityProfile Create(string isbn13)
    {
        if (!Isbn13Check.IsCanonical(isbn13))
        {
            throw new ArgumentException("A canonical ISBN-13 is required.", nameof(isbn13));
        }

        return new BookSimilarityProfile(isbn13);
    }

    public void RecordNotice(string? noticeJson, bool found, DateTime fetchedAt)
    {
        NoticeJson = noticeJson;
        NoticeFound = found;
        NoticeFetchedAt = DomainTime.RequireUtc(fetchedAt, nameof(fetchedAt));
    }

    public void SetProfileText(string text, string textHash)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(text);
        ArgumentException.ThrowIfNullOrWhiteSpace(textHash);
        ProfileText = text;
        ProfileTextHash = textHash;
    }

    public void RecordEmbedding(byte[] embedding, string textHash, DateTime embeddedAt)
    {
        ArgumentNullException.ThrowIfNull(embedding);
        if (embedding.Length != EmbeddingBytes)
        {
            throw new ArgumentException($"An embedding must hold {Dimensions} float32 values.", nameof(embedding));
        }

        Embedding = embedding;
        EmbeddedTextHash = textHash;
        EmbeddedAt = DomainTime.RequireUtc(embeddedAt, nameof(embeddedAt));
    }
}
