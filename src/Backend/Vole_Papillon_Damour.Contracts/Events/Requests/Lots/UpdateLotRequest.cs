using Microsoft.AspNetCore.Http;

namespace Vole_Papillon_Damour.Contracts.Events.Requests.Lots;

public class UpdateLotRequest
{
    public string? Name { get; set; }
    public IFormFile? Image { get; set; }
    public Uri? ImageUri { get; set; }
}