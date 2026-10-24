using System.Globalization;
using System.Net.Http.Headers;

namespace Vole_Papillon_Damour.Infrastructure.Services.Isbndb;

public sealed record IsbndbRateLimit(int? Limit, int? Remaining, DateTime? ResetsAt);

public static class IsbndbRateLimitHeader
{
    public static bool TryParse(
        HttpResponseHeaders headers,
        DateTime now,
        out IsbndbRateLimit result)
    {
        var rateEntries = ReadEntries(headers, "ratelimit");
        var policyEntries = ReadEntries(headers, "ratelimit-policy");
        var dailyRate = rateEntries.FirstOrDefault(entry => entry.Name == "daily");
        var dailyPolicy = policyEntries.FirstOrDefault(entry => entry.Name == "daily");

        int? remaining = TryGetNonNegativeInteger(dailyRate, "r", out var parsedRemaining)
            ? parsedRemaining
            : null;
        DateTime? resetsAt = TryGetNonNegativeInteger(dailyRate, "t", out var seconds)
            ? DateTime.SpecifyKind(now, DateTimeKind.Utc).AddSeconds(seconds)
            : null;
        int? limit = TryGetPositiveInteger(dailyPolicy, "q", out var parsedLimit)
            ? parsedLimit
            : null;

        result = new IsbndbRateLimit(limit, remaining, resetsAt);
        return limit is not null || remaining is not null || resetsAt is not null;
    }

    private static IReadOnlyList<RateLimitEntry> ReadEntries(HttpResponseHeaders headers, string name)
    {
        if (!headers.TryGetValues(name, out var values))
        {
            return [];
        }

        return values
            .SelectMany(value => value.Split(',', StringSplitOptions.RemoveEmptyEntries))
            .Select(ParseEntry)
            .Where(entry => entry is not null)
            .Select(entry => entry!)
            .ToArray();
    }

    private static RateLimitEntry? ParseEntry(string value)
    {
        var parts = value.Split(';', StringSplitOptions.TrimEntries);
        if (parts.Length == 0)
        {
            return null;
        }

        var name = parts[0].Trim().Trim('"');
        if (string.IsNullOrWhiteSpace(name))
        {
            return null;
        }

        var parameters = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var part in parts.Skip(1))
        {
            var separator = part.IndexOf('=');
            if (separator <= 0 || separator == part.Length - 1)
            {
                continue;
            }

            var key = part[..separator].Trim();
            var parameterValue = part[(separator + 1)..].Trim().Trim('"');
            parameters[key] = parameterValue;
        }

        return new RateLimitEntry(name, parameters);
    }

    private static bool TryGetNonNegativeInteger(
        RateLimitEntry? entry,
        string name,
        out int result)
    {
        result = 0;
        return entry is not null &&
               entry.Parameters.TryGetValue(name, out var value) &&
               int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out result) &&
               result >= 0;
    }

    private static bool TryGetPositiveInteger(
        RateLimitEntry? entry,
        string name,
        out int result)
    {
        result = 0;
        return entry is not null &&
               entry.Parameters.TryGetValue(name, out var value) &&
               int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out result) &&
               result > 0;
    }

    private sealed record RateLimitEntry(string Name, IReadOnlyDictionary<string, string> Parameters);
}
