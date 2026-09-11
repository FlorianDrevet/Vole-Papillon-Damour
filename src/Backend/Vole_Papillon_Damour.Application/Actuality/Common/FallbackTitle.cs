using System.Globalization;

namespace Vole_Papillon_Damour.Application.Actuality.Common;

public static class FallbackTitle
{
    public static string Create(DateTimeOffset publicationDate)
    {
        return $"Actualité du {publicationDate.ToString("d MMMM yyyy", CultureInfo.GetCultureInfo("fr-FR"))}";
    }
}
