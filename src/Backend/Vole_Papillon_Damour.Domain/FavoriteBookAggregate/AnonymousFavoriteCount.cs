using Vole_Papillon_Damour.Domain.BookAggregate.ValueObjects;
using Vole_Papillon_Damour.Domain.Common;
using Vole_Papillon_Damour.Domain.Common.Models;

namespace Vole_Papillon_Damour.Domain.FavoriteBookAggregate;

public sealed class AnonymousFavoriteCount : AggregateRoot<Isbn13>
{
    public Isbn13 Isbn13 => Id;
    public int Count { get; private set; }
    public DateTime UpdatedAt { get; private set; }

    private AnonymousFavoriteCount(Isbn13 isbn13, DateTime createdAt) : base(isbn13)
    {
        if (string.IsNullOrWhiteSpace(isbn13.Value))
        {
            throw new ArgumentException("A valid ISBN-13 is required.", nameof(isbn13));
        }

        UpdatedAt = DomainTime.RequireUtc(createdAt, nameof(createdAt));
    }

    public AnonymousFavoriteCount()
    {
    }

    public static AnonymousFavoriteCount Create(Isbn13 isbn13, DateTime createdAt)
    {
        return new AnonymousFavoriteCount(isbn13, createdAt);
    }

    public void Increment(DateTime at)
    {
        var utcAt = DomainTime.RequireUtc(at, nameof(at));
        if (Count < int.MaxValue)
        {
            Count++;
        }

        UpdatedAt = utcAt;
    }

    public void Decrement(DateTime at)
    {
        var utcAt = DomainTime.RequireUtc(at, nameof(at));
        if (Count > 0)
        {
            Count--;
        }

        UpdatedAt = utcAt;
    }
}
