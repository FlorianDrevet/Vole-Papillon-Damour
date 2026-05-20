namespace Vole_Papillon_Damour.Contracts.Actuality.Responses;

public class ActualityResponse
{
    public Guid Id { get; set; }
    public string Title { get; set; } = null!;
    public string Article { get; set; } = null!;
    public Uri UrlPrincipalImage { get; set; } = null!;
    public Uri? FacebookLink { get; set; }
    public Uri? InstagramLink { get; set; }
    public List<Uri> Images { get; set; } = null!;
    public DateTimeOffset Date { get; set; }
}