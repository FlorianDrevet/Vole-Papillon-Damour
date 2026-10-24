using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Threading.RateLimiting;
using Vole_Papillon_Damour.Application.Common.Interfaces.Services;
using Vole_Papillon_Damour.Application.Isbndb;
using Vole_Papillon_Damour.Domain.BookAggregate.ValueObjects;
using Vole_Papillon_Damour.Domain.IsbndbAggregate;

namespace Vole_Papillon_Damour.Infrastructure.Services.Isbndb;

public sealed class IsbndbClient(
    HttpClient httpClient,
    IOptions<IsbndbOptions> options,
    IIsbndbQuotaGuard quotaGuard,
    RateLimiter rateLimiter,
    TimeProvider timeProvider,
    ILogger<IsbndbClient> logger) : IIsbndbClient
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly IsbndbOptions _options = options.Value;

    public async Task<IsbndbLookup> GetBookAsync(
        Isbn13 isbn13,
        IsbndbUsage usage,
        CancellationToken cancellationToken)
    {
        if (!Isbn13.TryCreate(isbn13.Value, out var validIsbn))
        {
            return new IsbndbLookup.Unavailable("ISBN-13 invalide");
        }

        var response = await SendAsync(
            () => new HttpRequestMessage(HttpMethod.Get, $"book/{validIsbn.Value}"),
            cancellationToken);

        if (response.Status == ApiCallStatus.NotFound)
        {
            return new IsbndbLookup.NotFound();
        }

        if (response.Status == ApiCallStatus.QuotaExhausted)
        {
            return new IsbndbLookup.QuotaExhausted(response.ResetsAt);
        }

        if (response.Status != ApiCallStatus.Success)
        {
            return new IsbndbLookup.Unavailable(response.Reason ?? "ISBNdb indisponible");
        }

        if (!TryReadSingleBook(response.Body!, out var book))
        {
            await RecordFailureAsync("Réponse ISBNdb invalide", cancellationToken);
            return new IsbndbLookup.Unavailable("Réponse ISBNdb invalide");
        }

        if (string.IsNullOrWhiteSpace(book.Isbn13))
        {
            book = book with { Isbn13 = validIsbn.Value };
        }

        await quotaGuard.RecordUsageAsync(usage, 1, cancellationToken);
        return new IsbndbLookup.Found(book);
    }

    public async Task<IsbndbBulkLookup> GetBooksAsync(
        IReadOnlyList<Isbn13> isbn13s,
        IsbndbUsage usage,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(isbn13s);
        if (_options.BulkSize <= 0)
        {
            return new IsbndbBulkLookup(
                new Dictionary<Isbn13, IsbndbBook>(),
                [],
                IsbndbBulkLookupStatus.Unavailable,
                Reason: "ISBNdb BulkSize doit être positif");
        }

        var validIsbns = isbn13s
            .Select(isbn => Isbn13.TryCreate(isbn.Value, out var valid) ? valid : (Isbn13?)null)
            .Where(isbn => isbn is not null)
            .Select(isbn => isbn!.Value)
            .Distinct()
            .ToArray();
        var found = new Dictionary<Isbn13, IsbndbBook>();
        var missing = new List<Isbn13>();

        var bulkSize = Math.Min(_options.BulkSize, 100);
        for (var offset = 0; offset < validIsbns.Length; offset += bulkSize)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var batch = validIsbns.Skip(offset).Take(bulkSize).ToArray();
            var requestBody = JsonSerializer.Serialize(new { isbns = batch.Select(isbn => isbn.Value) }, JsonOptions);
            var response = await SendAsync(
                () => new HttpRequestMessage(HttpMethod.Post, "books")
                {
                    Content = new StringContent(requestBody, Encoding.UTF8, "application/json")
                },
                cancellationToken);

            if (response.Status == ApiCallStatus.NotFound)
            {
                missing.AddRange(batch);
                continue;
            }

            if (response.Status == ApiCallStatus.QuotaExhausted)
            {
                missing.AddRange(batch);
                missing.AddRange(validIsbns.Skip(offset + batch.Length));
                return new IsbndbBulkLookup(
                    found,
                    missing,
                    IsbndbBulkLookupStatus.QuotaExhausted,
                    response.ResetsAt);
            }

            if (response.Status != ApiCallStatus.Success)
            {
                missing.AddRange(batch);
                missing.AddRange(validIsbns.Skip(offset + batch.Length));
                return new IsbndbBulkLookup(
                    found,
                    missing,
                    IsbndbBulkLookupStatus.Unavailable,
                    Reason: response.Reason);
            }

            if (!TryReadBooks(response.Body!, out var books))
            {
                await RecordFailureAsync("Réponse ISBNdb invalide", cancellationToken);
                missing.AddRange(batch);
                missing.AddRange(validIsbns.Skip(offset + batch.Length));
                return new IsbndbBulkLookup(
                    found,
                    missing,
                    IsbndbBulkLookupStatus.Unavailable,
                    Reason: "Réponse ISBNdb invalide");
            }

            var requested = batch.ToHashSet();
            foreach (var book in books)
            {
                if (book.TryGetIsbn13(out var returnedIsbn) && requested.Contains(returnedIsbn))
                {
                    found[returnedIsbn] = book;
                }
            }

            missing.AddRange(batch.Where(isbn => !found.ContainsKey(isbn)));
            if (found.Count > 0)
            {
                var foundInBatch = batch.Count(isbn => found.ContainsKey(isbn));
                if (foundInBatch > 0)
                {
                    await quotaGuard.RecordUsageAsync(usage, foundInBatch, cancellationToken);
                }
            }
        }

        return new IsbndbBulkLookup(found, missing);
    }

    public async Task<IsbndbSearch> SearchAsync(
        string text,
        IsbndbUsage usage,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return new IsbndbSearch([], 0, IsbndbSearchStatus.Unavailable, Reason: "Recherche vide");
        }

        var response = await SendAsync(
            () => new HttpRequestMessage(
                HttpMethod.Get,
                $"books/{Uri.EscapeDataString(text.Trim())}?page=1&pageSize=100&language=fr"),
            cancellationToken);

        if (response.Status == ApiCallStatus.QuotaExhausted)
        {
            return new IsbndbSearch([], 0, IsbndbSearchStatus.QuotaExhausted, response.ResetsAt);
        }

        if (response.Status != ApiCallStatus.Success)
        {
            return new IsbndbSearch(
                [],
                0,
                IsbndbSearchStatus.Unavailable,
                Reason: response.Reason ?? "ISBNdb indisponible");
        }

        if (!TryReadSearch(response.Body!, out var books, out var total))
        {
            await RecordFailureAsync("Réponse ISBNdb invalide", cancellationToken);
            return new IsbndbSearch([], 0, IsbndbSearchStatus.Unavailable, Reason: "Réponse ISBNdb invalide");
        }

        await quotaGuard.RecordUsageAsync(usage, 1, cancellationToken);
        return new IsbndbSearch(books, total);
    }

    private async Task<ApiCallResult> SendAsync(
        Func<HttpRequestMessage> createRequest,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(_options.ApiKey))
        {
            return new ApiCallResult(ApiCallStatus.Unavailable, Reason: "ISBNdb non configuré");
        }

        var now = UtcNow();
        try
        {
            if (await quotaGuard.IsExhaustedAsync(now, cancellationToken))
            {
                return new ApiCallResult(ApiCallStatus.QuotaExhausted);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch
        {
            logger.LogWarning("ISBNdb quota status could not be read; the request was skipped.");
            return new ApiCallResult(ApiCallStatus.Unavailable, Reason: "Quota ISBNdb indisponible");
        }

        var maxRetries = Math.Clamp(_options.MaxRetries, 0, 3);
        for (var retry = 0; ; retry++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            using var lease = await rateLimiter.AcquireAsync(1, cancellationToken);
            if (!lease.IsAcquired)
            {
                return await FailAsync("Limiteur de débit ISBNdb saturé", cancellationToken);
            }

            using var request = createRequest();
            request.Headers.TryAddWithoutValidation("Authorization", _options.ApiKey);

            HttpResponseMessage response;
            try
            {
                response = await httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (OperationCanceledException)
            {
                return await FailAsync("Délai ISBNdb dépassé", cancellationToken);
            }
            catch (HttpRequestException)
            {
                return await FailAsync("Réseau ISBNdb indisponible", cancellationToken);
            }

            using (response)
            {
                var responseAt = UtcNow();
                var rateLimit = await ObserveQuotaAsync(response, responseAt, cancellationToken);
                string body;
                try
                {
                    body = await response.Content.ReadAsStringAsync(cancellationToken);
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    throw;
                }
                catch (Exception)
                {
                    return await FailAsync("Réponse ISBNdb illisible", cancellationToken, response.StatusCode);
                }

                if (response.StatusCode is HttpStatusCode.TooManyRequests or HttpStatusCode.Forbidden &&
                    IsQuotaExhausted(rateLimit, body))
                {
                    var resetsAt = rateLimit?.ResetsAt ?? NextUtcMidnight(responseAt);
                    await quotaGuard.MarkExhaustedAsync(resetsAt, responseAt, cancellationToken);
                    return new ApiCallResult(ApiCallStatus.QuotaExhausted, ResetsAt: resetsAt);
                }

                if (response.StatusCode == HttpStatusCode.NotFound)
                {
                    return new ApiCallResult(ApiCallStatus.NotFound);
                }

                if (response.StatusCode == HttpStatusCode.TooManyRequests)
                {
                    if (retry < maxRetries)
                    {
                        var delay = RetryDelay(response.Headers.RetryAfter, retry, responseAt);
                        await Task.Delay(delay, timeProvider, cancellationToken);
                        continue;
                    }

                    return await FailAsync("ISBNdb a refusé la requête (HTTP 429)", cancellationToken, response.StatusCode);
                }

                if (!response.IsSuccessStatusCode)
                {
                    return await FailAsync(
                        $"ISBNdb a répondu HTTP {(int)response.StatusCode}",
                        cancellationToken,
                        response.StatusCode);
                }

                return new ApiCallResult(ApiCallStatus.Success, Body: body, ResetsAt: rateLimit?.ResetsAt);
            }
        }
    }

    private async Task<IsbndbRateLimit?> ObserveQuotaAsync(
        HttpResponseMessage response,
        DateTime observedAt,
        CancellationToken cancellationToken)
    {
        if (!IsbndbRateLimitHeader.TryParse(response.Headers, observedAt, out var rateLimit))
        {
            return null;
        }

        if (rateLimit.Limit is { } limit &&
            rateLimit.Remaining is { } remaining &&
            rateLimit.ResetsAt is { } resetsAt)
        {
            await quotaGuard.ObserveAsync(limit, remaining, resetsAt, observedAt, cancellationToken);
        }

        return rateLimit;
    }

    private async Task<ApiCallResult> FailAsync(
        string reason,
        CancellationToken cancellationToken,
        HttpStatusCode? statusCode = null)
    {
        var safeReason = statusCode is null ? reason : $"{reason}: {(int)statusCode.Value}";
        logger.LogWarning("ISBNdb request failed ({FailureReason}).", safeReason);
        await RecordFailureAsync(safeReason, cancellationToken);
        return new ApiCallResult(ApiCallStatus.Unavailable, Reason: reason);
    }

    private Task RecordFailureAsync(string reason, CancellationToken cancellationToken) =>
        quotaGuard.RecordFailureAsync(reason, UtcNow(), cancellationToken);

    private DateTime UtcNow() => timeProvider.GetUtcNow().UtcDateTime;

    private static bool IsQuotaExhausted(IsbndbRateLimit? rateLimit, string body) =>
        rateLimit?.Remaining == 0 ||
        body.Contains("limit", StringComparison.OrdinalIgnoreCase) ||
        body.Contains("quota", StringComparison.OrdinalIgnoreCase);

    private static DateTime NextUtcMidnight(DateTime now) =>
        DateTime.SpecifyKind(now.Date.AddDays(1), DateTimeKind.Utc);

    private static TimeSpan RetryDelay(RetryConditionHeaderValue? retryAfter, int retry, DateTime now)
    {
        if (retryAfter?.Delta is { } delta && delta > TimeSpan.Zero)
        {
            return delta;
        }

        if (retryAfter?.Date is { } retryAt)
        {
            var untilRetry = retryAt.UtcDateTime - now;
            if (untilRetry > TimeSpan.Zero)
            {
                return untilRetry;
            }
        }

        var seconds = Math.Pow(2, Math.Min(retry, 20));
        return TimeSpan.FromSeconds(seconds);
    }

    private static bool TryReadSingleBook(string json, out IsbndbBook book)
    {
        book = null!;
        try
        {
            using var document = JsonDocument.Parse(json);
            var element = document.RootElement;
            if (element.ValueKind == JsonValueKind.Object && element.TryGetProperty("data", out var data))
            {
                element = data.ValueKind == JsonValueKind.Array
                    ? data.EnumerateArray().FirstOrDefault()
                    : data;
            }
            else if (element.ValueKind == JsonValueKind.Object && element.TryGetProperty("book", out var bookEnvelope))
            {
                element = bookEnvelope;
            }

            if (element.ValueKind != JsonValueKind.Object)
            {
                return false;
            }

            var sanitizedJson = SanitizeJson(element.GetRawText());
            var parsed = JsonSerializer.Deserialize<IsbndbBook>(sanitizedJson, JsonOptions);
            if (parsed is null)
            {
                return false;
            }

            book = parsed with { RawJson = sanitizedJson };
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static bool TryReadBooks(string json, out IReadOnlyList<IsbndbBook> books)
    {
        books = [];
        try
        {
            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;
            if (!TryGetBookArray(root, out var data))
            {
                return false;
            }

            var result = new List<IsbndbBook>();
            foreach (var element in data.EnumerateArray())
            {
                if (element.ValueKind != JsonValueKind.Object)
                {
                    continue;
                }

                var sanitizedJson = SanitizeJson(element.GetRawText());
                var book = JsonSerializer.Deserialize<IsbndbBook>(sanitizedJson, JsonOptions);
                if (book is not null)
                {
                    result.Add(book with { RawJson = sanitizedJson });
                }
            }

            books = result;
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static bool TryReadSearch(string json, out IReadOnlyList<IsbndbBook> books, out int total)
    {
        books = [];
        total = 0;
        try
        {
            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object || !TryGetBookArray(root, out var data))
            {
                return false;
            }

            total = root.TryGetProperty("total", out var totalValue) && totalValue.TryGetInt32(out var parsedTotal)
                ? Math.Max(0, parsedTotal)
                : data.GetArrayLength();
            return TryReadBooks($"{{\"data\":{data.GetRawText()}}}", out books);
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static bool TryGetBookArray(JsonElement root, out JsonElement data)
    {
        data = default;
        if (root.ValueKind != JsonValueKind.Object)
        {
            return false;
        }

        foreach (var name in new[] { "data", "books", "results" })
        {
            if (root.TryGetProperty(name, out data))
            {
                return data.ValueKind == JsonValueKind.Array;
            }
        }

        return false;
    }

    private static string SanitizeJson(string json)
    {
        var node = JsonNode.Parse(json);
        RemoveImageOriginal(node);
        return node?.ToJsonString(JsonOptions) ?? "{}";
    }

    private static void RemoveImageOriginal(JsonNode? node)
    {
        if (node is JsonObject jsonObject)
        {
            foreach (var key in jsonObject.Select(pair => pair.Key).ToArray())
            {
                if (key.Equals("image_original", StringComparison.OrdinalIgnoreCase))
                {
                    jsonObject.Remove(key);
                }
                else
                {
                    RemoveImageOriginal(jsonObject[key]);
                }
            }
        }
        else if (node is JsonArray jsonArray)
        {
            foreach (var item in jsonArray)
            {
                RemoveImageOriginal(item);
            }
        }
    }

    private enum ApiCallStatus : byte
    {
        Success,
        NotFound,
        QuotaExhausted,
        Unavailable
    }

    private sealed record ApiCallResult(
        ApiCallStatus Status,
        string? Body = null,
        DateTime? ResetsAt = null,
        string? Reason = null);
}
