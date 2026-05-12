using Microsoft.AspNetCore.Http;

namespace Vole_Papillon_Damour.Contracts.Events.Requests;

public class CreateEventRequest
{
    public string? Name { get; set; }
    public IFormFile? Image { get; set; }
    public string? EventType { get; set; }
    public DateTimeOffset? DateStart { get; set; }
    public DateTimeOffset? DateEnd { get; set; }
    public DateTimeOffset? HourOpenDoors { get; set; }
    public DateTimeOffset? HourCloseDoors { get; set; }
    public Uri? UrlRegistration { get; set; }
    public IFormFile? ImageMap { get; set; }
    public string? Description { get; set; }
    public int? RoadNumber { get; set; }
    public string? City { get; set; }
    public int? CityCode { get; set; }
    public string? Road { get; set; }
    public CreatePartiesRequest[]? Parties { get; set; }
}

public class CreatePartiesRequest
{
    public string? Name { get; set; }
    public string? PartieType { get; set; }
    public bool? PauseAfter { get; set; }
    public int? Index { get; set; }
    public List<CreateLinePartieRequest>? LineParties { get; set; } 
}

public class CreateLinePartieRequest
{
    public List<CreateLotsRequest>? Lots { get; set; }
    public string? NumberLine { get; set; }
    public int? Index { get; set; }
}

public class CreateLotsRequest
{
    public string? Name { get; set; }
    public IFormFile? Image { get; set; }
    public int? Index { get; set; }
}