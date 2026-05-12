using Microsoft.AspNetCore.Http;

namespace Vole_Papillon_Damour.Contracts.Actuality.Requests;

public class UpdateActualityRequest
{
    public string Title { get; set; } = null!;
    public string Article { get; set; } = null!;
    public IFormFile? PrincipalImage { get; set; }
    public Uri? PrincipalImageUri { get; set; }
    public Uri? FacebookLink { get; set; } = null!;
    public Uri? InstagramLink { get; set; } = null!;
    public List<IFormFile> Images { get; set; } = null!;
    public List<Uri> ImagesUrls { get; set; } = null!;
    public DateTimeOffset Date { get; set; }
}