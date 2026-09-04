namespace Vole_Papillon_Damour.Api.Common;

public static class DatabaseMigrationPolicy
{
    public static bool ShouldRunOnStartup(string? environmentName)
    {
        return string.Equals(
            environmentName,
            "Development",
            StringComparison.OrdinalIgnoreCase);
    }
}
