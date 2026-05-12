using Vole_Papillon_Damour.Domain.AssoEventsAggregate.ValueObjects;
using Vole_Papillon_Damour.Domain.Common.Models;
using Vole_Papillon_Damour.Domain.EventsAggregate.ValueObjects;

namespace Vole_Papillon_Damour.Domain.AssoEventsAggregate.Entities;

public sealed class Lot : Entity<LotId>
{
    public string Name { get; private set; } = null!;
    public string UrlImage { get; private set; } = null!;
    public int Index { get; set; }
    public int? IsWon { get; set; }

    private Lot(LotId id, string name, string urlImage, int index, int? isWon)
        : base(id)
    {
        Name = name;
        UrlImage = urlImage;
        Index = index;
        IsWon = isWon;
    }

    public static Lot Create(string name, string urlImage, int index, int? isWon = null)
    {
        return new Lot(LotId.CreateUnique(), name, urlImage, index, isWon);
    }

    public Lot()
    {
    }

    public void Update(string name, string urlImage)
    {
        Name = name;
        UrlImage = urlImage;
    }
    
    public bool IsWonByLastNumber(int lastNumber)
    {
        return IsWon == lastNumber;
    }
}