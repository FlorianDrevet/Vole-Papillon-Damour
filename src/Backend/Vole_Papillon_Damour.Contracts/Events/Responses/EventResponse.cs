namespace Vole_Papillon_Damour.Contracts.Events.Responses;

public class EventResponse
{
    public Guid Id { get; set; }
    public string Name { get; set; }
    public Uri? UrlImage { get; set; }
    public string EventType { get; set; }
    public DateTimeOffset DateStart { get; set; }
    public DateTimeOffset? DateEnd { get; set; }
    public DateTimeOffset? HourOpenDoors { get; set; }
    public DateTimeOffset? HourCloseDoors { get; set; }
    public Uri? UrlRegistration { get; set; }
    public Uri? UrlImageMap { get; set; }
    public string Description { get; set; }
    public int? RoadNumber { get; set; }
    public string City { get; set; }
    public int CityCode { get; set; }
    public string Road { get; set; }
    
    // when set order by Index
    private List<CreatePartiesResponse>? _parties;
    public List<CreatePartiesResponse>? Parties { 
        get => _parties;
        set => _parties = value?.OrderBy(p => p.Index).ToList();
    }
    
    public List<int> BingoNumeros { get; set; }
    public bool BingoHasBeenWon { get; set; }
    public int CurrentPartieIndex { get; set; }
}

public class CreatePartiesResponse
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string PartieType { get; set; } = string.Empty;
    public int Index { get; set; }
    public bool PauseAfter { get; set; }
    public int? AddedBingoNumber { get; set; }
    public int CurrentLineIndex { get; set; }
    public List<int> LastNumeros { get; set; } = [];
    public List<int> LiveNumeros { get; set; } = [];
    public List<LinePartieResponse> LineParties { get; set; } = [];
}

public class LinePartieResponse
{
    public Guid Id { get; set; }
    public List<LotsResponse> Lots { get; set; } = [];
    public string NumberLine { get; set; } = string.Empty;
    public int Index { get; set; }
}

public class LotsResponse
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string UrlImage { get; set; } = string.Empty;
    public int Index { get; set; }
    public int? IsWon { get; set; }
}