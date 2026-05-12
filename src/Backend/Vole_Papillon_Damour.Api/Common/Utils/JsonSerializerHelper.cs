using System.Text.Json;

namespace Vole_Papillon_Damour.Api.Common.Utils;

public static class JsonSerializerHelper
{
    private static readonly JsonSerializerOptions _options = new JsonSerializerOptions
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public static string Serialize<T>(T obj)
    {
        return JsonSerializer.Serialize(obj, _options);
    }
}