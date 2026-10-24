using System.Text.Json;
using Vole_Papillon_Damour.Domain.BookAggregate;

namespace Vole_Papillon_Damour.Application.Isbndb;

public static class IsbndbSubjectReader
{
    private static readonly HashSet<string> SubjectPropertyNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "subject", "subjects", "dc:subject", "categories"
    };

    public static IReadOnlyList<string> Read(Book book, string? recordSubjects)
    {
        ArgumentNullException.ThrowIfNull(book);
        var stored = ReadStringArray(recordSubjects);
        if (stored.Count > 0) return stored;
        return ReadFallbackPayload(book.RawPayload);
    }

    private static IReadOnlyList<string> ReadStringArray(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return [];
        try
        {
            using var document = JsonDocument.Parse(json);
            return ReadValues(document.RootElement).Distinct(StringComparer.OrdinalIgnoreCase).Take(50).ToArray();
        }
        catch (JsonException)
        {
            return [];
        }
    }

    private static IReadOnlyList<string> ReadFallbackPayload(string? rawPayload)
    {
        if (string.IsNullOrWhiteSpace(rawPayload)) return [];
        try
        {
            using var document = JsonDocument.Parse(rawPayload);
            var subjects = new List<string>();
            FindSubjects(document.RootElement, subjects);
            return subjects.Distinct(StringComparer.OrdinalIgnoreCase).Take(50).ToArray();
        }
        catch (JsonException)
        {
            return [];
        }
    }

    private static void FindSubjects(JsonElement element, ICollection<string> results)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            foreach (var property in element.EnumerateObject())
            {
                if (SubjectPropertyNames.Contains(property.Name))
                {
                    foreach (var value in ReadValues(property.Value)) results.Add(value);
                }
                else if (property.Value.ValueKind is JsonValueKind.Object or JsonValueKind.Array)
                {
                    FindSubjects(property.Value, results);
                }
            }
        }
        else if (element.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in element.EnumerateArray()) FindSubjects(item, results);
        }
    }

    private static IEnumerable<string> ReadValues(JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.String)
        {
            var value = element.GetString()?.Trim();
            if (!string.IsNullOrWhiteSpace(value)) yield return value;
            yield break;
        }

        if (element.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in element.EnumerateArray())
            foreach (var value in ReadValues(item)) yield return value;
            yield break;
        }

        if (element.ValueKind == JsonValueKind.Object)
        {
            foreach (var key in new[] { "name", "label", "value", "@value" })
            {
                if (element.TryGetProperty(key, out var nested))
                foreach (var value in ReadValues(nested)) yield return value;
            }
        }
    }
}
