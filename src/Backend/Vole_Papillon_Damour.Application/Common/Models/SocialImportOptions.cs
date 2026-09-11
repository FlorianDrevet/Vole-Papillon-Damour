namespace Vole_Papillon_Damour.Application.Common.Models;

public sealed class SocialImportOptions
{
    public const string SectionName = "SocialImport";

    public DateTimeOffset ImportFloorDate { get; set; } = DateTimeOffset.MinValue;
    public int MaxPostsPerRun { get; set; } = 5;
}
