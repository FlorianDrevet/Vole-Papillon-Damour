namespace Vole_Papillon_Damour.Infrastructure.Services.OcrService;

public class OcrSettings
{
    public const string SectionName = "OcrSettings";
    public string VisionKey { get; init; } = null!;
    public string VisionEndpoint { get; init; } = null!;
}