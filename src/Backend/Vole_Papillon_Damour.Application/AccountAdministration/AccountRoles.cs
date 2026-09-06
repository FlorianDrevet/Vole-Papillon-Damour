namespace Vole_Papillon_Damour.Application.AccountAdministration;

public static class AccountRoles
{
    public const string Tri = "Tri";
    public const string Caisse = "Caisse";
    public const string Administration = "Administration";

    public static IReadOnlyList<string> Normalize(IEnumerable<string>? roles)
    {
        var normalized = new List<string>();
        foreach (var role in roles ?? [])
        {
            var canonical = role?.Trim() switch
            {
                var value when string.Equals(value, Tri, StringComparison.OrdinalIgnoreCase) => Tri,
                var value when string.Equals(value, Caisse, StringComparison.OrdinalIgnoreCase) => Caisse,
                var value when string.Equals(value, Administration, StringComparison.OrdinalIgnoreCase) => Administration,
                _ => null
            };

            if (canonical is null)
            {
                return [];
            }

            if (!normalized.Contains(canonical, StringComparer.Ordinal))
            {
                normalized.Add(canonical);
            }
        }

        return normalized;
    }

    public static bool IsValid(IEnumerable<string>? roles)
    {
        var source = roles?.ToArray();
        return source is not null && source.All(role => role?.Trim() switch
        {
            var value when string.Equals(value, Tri, StringComparison.OrdinalIgnoreCase) => true,
            var value when string.Equals(value, Caisse, StringComparison.OrdinalIgnoreCase) => true,
            var value when string.Equals(value, Administration, StringComparison.OrdinalIgnoreCase) => true,
            _ => false
        });
    }
}
