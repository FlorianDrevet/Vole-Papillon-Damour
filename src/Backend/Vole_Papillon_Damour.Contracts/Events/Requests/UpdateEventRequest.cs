using Microsoft.AspNetCore.Http;

namespace Vole_Papillon_Damour.Contracts.Events.Requests;

public class UpdateEventRequest
{
    public string? Name { get; set; }
    public IFormFile? Image { get; set; }
    public Uri? ImageUri { get; set; }
    public string? EventType { get; set; }
    public DateTimeOffset? DateStart { get; set; }
    public DateTimeOffset? DateEnd { get; set; }
    public DateTimeOffset? HourOpenDoors { get; set; }
    public DateTimeOffset? HourCloseDoors { get; set; }
    public Uri? UrlRegistration { get; set; }
    public IFormFile? ImageMap { get; set; }
    public Uri? ImageMapUri { get; set; }
    public string? Description { get; set; }
    public int? RoadNumber { get; set; }
    public string? City { get; set; }
    public int? CityCode { get; set; }
    public string? Road { get; set; }
}