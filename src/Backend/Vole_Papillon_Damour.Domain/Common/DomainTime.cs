namespace Vole_Papillon_Damour.Domain.Common;

internal static class DomainTime
{
    public static DateTime RequireUtc(DateTime value, string parameterName)
    {
        if (value.Kind != DateTimeKind.Utc)
        {
            throw new ArgumentException("The timestamp must be expressed in UTC.", parameterName);
        }

        return value;
    }
}
