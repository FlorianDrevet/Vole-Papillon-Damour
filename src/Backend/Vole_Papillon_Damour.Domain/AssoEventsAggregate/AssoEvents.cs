using Vole_Papillon_Damour.Domain.AssoEventsAggregate.Entities;
using Vole_Papillon_Damour.Domain.AssoEventsAggregate.ValueObjects;
using Vole_Papillon_Damour.Domain.Common.Models;
using Vole_Papillon_Damour.Domain.EventsAggregate.ValueObjects;

namespace Vole_Papillon_Damour.Domain.AssoEventsAggregate;

public sealed class AssoEvents : AggregateRoot<AssoEventsId>
{
    public string Name { get; private set; } = null!;
    public Uri? UrlImage { get; set; } = null!;
    public EventsType EventsType { get; set; } = null!;
    public DateTimeOffset DateStart { get; set; }
    public DateTimeOffset? DateEnd { get; set; }
    public DateTimeOffset? HourOpenDoors { get; set; }
    public DateTimeOffset? HourCloseDoors { get; set; }
    public Uri? UrlRegistration { get; set; }
    public Uri? UrlImageMap { get; set; }
    public Adresse Adresse { get; set; } = null!;
    public string Description { get; set; } = null!;
    public bool BingoHasBeenWon { get; set; } = false;
    public bool IsCancelled { get; private set; }
    
    public int CurrentPartieIndex { get; set; } = 0;
    
    private IList<Partie>? _parties = new List<Partie>();
    public IReadOnlyList<Partie>? Parties => _parties?.AsReadOnly();
    
    private IList<int> _bingoNumeros = new List<int>();
    public IReadOnlyList<int> BingoNumeros => _bingoNumeros.AsReadOnly();

    private AssoEvents(AssoEventsId id,
        string name,
        Uri? urlImage,
        EventsType eventsType,
        DateTimeOffset dateStart,
        DateTimeOffset? dateEnd,
        DateTimeOffset? hourOpenDoors,
        DateTimeOffset? hourCloseDoors,
        Adresse adresse,
        string description,
        Uri? urlRegistration,
        Uri? urlImageMap,
        IList<int> bingoNumeros,
        IList<Partie> parties,
        bool bingoHasBeenWon,
        int currentPartieIndex)
        : base(id)
    {
        Name = name;
        EventsType = eventsType;
        DateStart = dateStart;
        DateEnd = dateEnd;
        Adresse = adresse;
        Description = description;
        HourOpenDoors = hourOpenDoors;
        HourCloseDoors = hourCloseDoors;
        UrlImageMap = urlImageMap;
        UrlRegistration = urlRegistration;
        BingoHasBeenWon = bingoHasBeenWon;
        _bingoNumeros = bingoNumeros;
        _parties = parties;
        CurrentPartieIndex = currentPartieIndex;
        UrlImage = urlImage;
    }

    public static AssoEvents Create(string name,
        Uri? urlImage,
        EventsType eventsType,
        DateTimeOffset dateStart,
        DateTimeOffset? dateEnd,
        DateTimeOffset? hourOpenDoors,
        DateTimeOffset? hourCloseDoors,
        Uri? urlImageMap,
        Adresse adresse,
        Uri? urlRegistration,
        IList<Partie> parties,
        string description)
    {
        return new AssoEvents(AssoEventsId.CreateUnique(),
            name, urlImage, eventsType, dateStart, dateEnd, hourOpenDoors, hourCloseDoors, adresse, description, urlRegistration, urlImageMap, [],
            parties, false, 0);
    }

    public AssoEvents()
    {
    }

    public bool Cancel()
    {
        if (IsCancelled)
        {
            return false;
        }

        IsCancelled = true;
        return true;
    }
    
    public void Update(string name,
        Uri? urlImage,
        EventsType eventsType,
        DateTimeOffset dateStart,
        DateTimeOffset? dateEnd,
        DateTimeOffset? hourOpenDoors,
        DateTimeOffset? hourCloseDoors,
        Uri? urlImageMap,
        Adresse adresse,
        string description,
        Uri? urlRegistration)
    {
        Name = name;
        UrlImage = urlImage;
        EventsType = eventsType;
        DateStart = dateStart;
        DateEnd = dateEnd;
        HourOpenDoors = hourOpenDoors;
        HourCloseDoors = hourCloseDoors;
        UrlImageMap = urlImageMap;
        Adresse = adresse;
        Description = description;
        UrlRegistration = urlRegistration;
    }

    public bool AddBingoNumero(int numero)
    {
        if (BingoHasBeenWon || _bingoNumeros.Contains(numero))
        {
            return false;
        }
        _bingoNumeros.Add(numero);
        return true;
    }

    public bool RemoveBingoNumero(int numero)
    {
        return _bingoNumeros.Remove(numero);
    }

    public bool DeletePartie(Partie partie)
    {
        var isRemoved = _parties?.Remove(partie) ?? false;
        if (!isRemoved)
        {
            return false;
        }
        var partiesAfter = _parties?.Where(p => p.Index >= partie.Index).ToList();
        if (partiesAfter is not null)
        {
            foreach (var partieAfter in partiesAfter)
            {
                partieAfter.Index--;
            }
        }
        return true;
    }

    //TODO checks
    public void AddPartie(Partie partie)
    {
        var partiesAfter = _parties?.Where(p => p.Index >= partie.Index).ToList();
        if (partiesAfter is not null)
        {
            foreach (var partieAfter in partiesAfter)
            {
                partieAfter.Index++;
            }
        } 
        var partiesBefore = _parties?.Where(p => p.Index < partie.Index).ToList();
        var allParties = partiesBefore?.Union(partiesAfter ?? new List<Partie>()).ToList();
        allParties?.Add(partie);
        _parties = allParties?.OrderBy(p => p.Index).ToList();
    }
}
