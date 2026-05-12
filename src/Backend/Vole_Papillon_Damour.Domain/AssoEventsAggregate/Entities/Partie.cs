using System.Collections;
using System.Reflection.Metadata.Ecma335;
using Vole_Papillon_Damour.Domain.AssoEventsAggregate.ValueObjects;
using Vole_Papillon_Damour.Domain.Common.Models;
using Vole_Papillon_Damour.Domain.EventsAggregate.ValueObjects;

namespace Vole_Papillon_Damour.Domain.AssoEventsAggregate.Entities;

public sealed class Partie : Entity<PartieId>
{
    public string? Name { get; private set; } = null!;
    public PartieType PartieType { get; private set; } = null!;
    public int Index { get; set; }
    public bool PauseAfter { get; private set; }
    public int? AddedBingoNumber { get; set; }
    public int CurrentLineIndex { get; set; }
    
    private IList<int> _lastNumeros = new List<int>();
    public IReadOnlyList<int> LastNumeros => _lastNumeros.AsReadOnly();
    
    private IList<int> _liveNumeros = new List<int>();
    public IReadOnlyList<int> LiveNumeros => _liveNumeros.AsReadOnly();

    private IList<LinePartie> _lineParties = new List<LinePartie>();
    public IReadOnlyList<LinePartie> LineParties => _lineParties.AsReadOnly();

    private Partie(PartieId id, string? name, PartieType partieType, int index, bool pauseAfter, IList<int> lastNumero,
       IList<LinePartie> lineParties, IList<int> liveNumeros, int? addedBingoNumber, int currentLineIndex)
        : base(id)
    {
        Name = name;
        this.PartieType = partieType;
        Index = index;
        PauseAfter = pauseAfter;
        _lastNumeros = lastNumero;
        _lineParties = lineParties;
        _liveNumeros = liveNumeros;
        AddedBingoNumber = addedBingoNumber;
        CurrentLineIndex = currentLineIndex;
    }

    public static Partie Create(string? name, PartieType partieType, int index, bool pauseAfter,
        IList<LinePartie> lineParties)
    {
        return new Partie(PartieId.CreateUnique(),
            name,
            partieType,
            index,
            pauseAfter,
            [],
            lineParties,
            [],
            null,
            0
            );
    }
    
    public void Update(string name, PartieType partieType, bool pauseAfter)
    {
        Name = name;
        PartieType = partieType;
        PauseAfter = pauseAfter;
    }

    public Partie()
    { 
    }

    public void SetLastNumero(List<int> lastNumeros)
    {
        _lastNumeros = lastNumeros;
    }

    public void SetLiveNumeros(List<int> liveNumeros)
    {
        _liveNumeros = liveNumeros;
    }
    
    public bool AddLiveNumero(int numero)
    {
        if (PartieType.Value == PartieType.PartieTypeEnum.PlusUnMoinsUn)
        {
            if (_lastNumeros.Contains(numero))
            {
                return false;
            }
            _lastNumeros.Add(numero);
            _liveNumeros.Add(numero);
            
            var numeroBefore = numero - 1 < 1 ? 90 : numero - 1;
            if (!_liveNumeros.Contains(numeroBefore))
            {
                _liveNumeros.Add(numeroBefore);
            }

            var numeroAfter = numero + 1 > 90 ? 1 : numero + 1;
            if (!_liveNumeros.Contains(numeroAfter))
            {
                _liveNumeros.Add(numeroAfter);
            }
            return true;
        }
        
        if (_liveNumeros.Contains(numero))
        {
            return false;
        }

        _lastNumeros.Add(numero);
        _liveNumeros.Add(numero);
        return true;
    }

    /// <summary>
    /// Remove last numero and update CurrentLineIndex
    /// </summary>
    /// <returns>true if all numeros removed and no last numero</returns>
    public int? RemoveLastNumero()
    {
        if (!_lastNumeros.Any())
        {
            return null;
        }

        List<int> lastNumeros = new List<int>();
        
        int lastNumero = _lastNumeros.Last();
        _lastNumeros.RemoveAt(_lastNumeros.Count() - 1);

        //TODO check not found 
        while (_liveNumeros.Last() != lastNumero)
        {
            lastNumeros.Add(_liveNumeros.Last());
            _liveNumeros.RemoveAt(_liveNumeros.Count() - 1);
        }
        lastNumeros.Add(_liveNumeros.Last());
        _liveNumeros.RemoveAt(_liveNumeros.Count() - 1);

        var linePartie = _lineParties.First(l => l.Index == (CurrentLineIndex != 0 ? CurrentLineIndex - 1 : CurrentLineIndex));
        var hasANumberBeenDeleted = linePartie.RemoveNumero(lastNumeros);
        
        if (CurrentLineIndex != 0 && hasANumberBeenDeleted)
        {
            if (CurrentLineIndex >= _lineParties.Count())
            {
                CurrentLineIndex--;
            }
            else
            {
                CurrentLineIndex--;
                var linePartieBefore = _lineParties.First(l => l.Index == CurrentLineIndex);
                linePartieBefore.RemoveNumero(lastNumeros);
            }
        }

        return lastNumero;
    }

    /// <summary>
    /// Say that the last numero has won
    /// </summary>
    /// <returns>true if all lots has been won</returns>
    public bool AddWin()
    {
        var linePartie = _lineParties.First(l => l.Index == CurrentLineIndex);
        
        // Exit when click on win twice or then no numero clicked before
        if (linePartie.GetLastWinningNumber().Contains(_lastNumeros.Last()) || !_lastNumeros.Any())
            return false;

        if (linePartie.AddWin(_lastNumeros.Last()))
        {
            CurrentLineIndex++;
            if (CurrentLineIndex == _lineParties.Count())
            {
                return true;
            }
        }

        return false;
    }

    public void AddLinePartie(LinePartie linePartie)
    {
        var existingLinePartie = _lineParties.FirstOrDefault(x => x.Index == linePartie.Index);
        if (existingLinePartie is null)
        {
            _lineParties.Add(linePartie);
        }
        else
        {
            int indexExistingLinePartie = _lineParties.IndexOf(existingLinePartie);
            foreach (var lot in linePartie.Lots)
            {
                _lineParties[indexExistingLinePartie].AddLot(lot);
            }
        }
    }

    public bool DeleteLinePartie(LinePartieId commandLinePartieId)
    {
        var linePartie = _lineParties.ToList().Find(x => x.Id == commandLinePartieId);
        if (linePartie is null)
            return false;

        return _lineParties.Remove(linePartie);
    }
}