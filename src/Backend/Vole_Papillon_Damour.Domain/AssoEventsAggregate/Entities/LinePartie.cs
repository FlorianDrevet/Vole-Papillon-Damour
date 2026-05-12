using Vole_Papillon_Damour.Domain.AssoEventsAggregate.ValueObjects;
using Vole_Papillon_Damour.Domain.Common.Models;

namespace Vole_Papillon_Damour.Domain.AssoEventsAggregate.Entities;

public sealed class LinePartie : Entity<LinePartieId>
{

    private IList<Lot> _lots = new List<Lot>();
    public IReadOnlyList<Lot> Lots => _lots.AsReadOnly();
    public NumberLine NumberLine { get; private set; } = null!;
    public int NumberLotsToWin {get; private set;}
    public int Index { get; private set; }
    
    private LinePartie(LinePartieId id, IList<Lot> lots, NumberLine numberLine, int index)
        : base(id)
    {
        _lots = lots;
        NumberLine = numberLine;
        Index = index;
        if (numberLine.Value == NumberLine.NumberLineEnum.OneLine)
        {
            NumberLotsToWin = lots.Count;
        }
        else
        {
            NumberLotsToWin = 1;
        }
    }

    public static LinePartie Create(IList<Lot> lots, NumberLine numberLine, int? index = null)
    {
        return new LinePartie(LinePartieId.CreateUnique(), lots, numberLine, index ?? (int)numberLine.Value);
    }

    public LinePartie()
    {
    }
    
    public bool IsStillALotToWin()
    {
        return _lots.Count(l => l.IsWon is not null) >= NumberLotsToWin;
    }
    
    public List<int?> GetLastWinningNumber()
    {
        return _lots.Where(l => l.IsWon is not null).Select(l => l.IsWon).ToList();
    }

    /// <summary>
    /// Pass last number as won
    /// </summary>
    /// <param name="numero"></param>
    /// <returns>true if all lots has been won</returns>
    public bool AddWin(int numero)
    {
        var lots = _lots.Where(l => l.IsWon is null).ToList();
        if (lots.Count == 0)
        {
            return false;
        }

        lots.First().IsWon = numero;

        return IsStillALotToWin();
    }

    public bool RemoveNumero(List<int> numero)
    {
        var winningLotWithThisNumber = _lots.ToList().Find(l => numero.Contains(l.IsWon ?? -1));
        if (winningLotWithThisNumber is not null)
        {
            winningLotWithThisNumber.IsWon = null;
            return true;
        }

        return false;
    }

    public void AddLot(Lot lot)
    {
        _lots.Add(lot);
        
        if (NumberLine.Value == NumberLine.NumberLineEnum.OneLine)
        {
            NumberLotsToWin = _lots.Count;
        }
    }

    public bool DeleteLot(LotId lineCommandLotId)
    {
        var lot = _lots.ToList().Find(l => l.Id == lineCommandLotId);
        if (lot is null)
        {
            return false;
        }

        return _lots.Remove(lot);
    }
}