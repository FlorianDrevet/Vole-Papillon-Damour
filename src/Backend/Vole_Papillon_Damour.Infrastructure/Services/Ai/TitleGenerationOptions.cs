namespace Vole_Papillon_Damour.Infrastructure.Services.Ai;

public sealed class TitleGenerationOptions
{
    public const string SectionName = "TitleGeneration";

    public string Endpoint { get; set; } = string.Empty;
    public string DeploymentName { get; set; } = string.Empty;
    public float Temperature { get; set; } = 0.2f;
    public int MaxOutputTokens { get; set; } = 40;
    public int TimeoutMilliseconds { get; set; } = 10_000;
}
