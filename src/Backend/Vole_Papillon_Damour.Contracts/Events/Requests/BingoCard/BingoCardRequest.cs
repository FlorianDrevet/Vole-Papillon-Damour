using Microsoft.AspNetCore.Http;

namespace Vole_Papillon_Damour.Contracts.Events.Requests.BingoCard;

public class BingoCardRequest
{
    public IFormFile? Image { get; set; }
    public Guid? EventId { get; set; }
}