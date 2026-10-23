using System.Net;
using System.Diagnostics;
using System.Net.Http.Headers;
using System.Text.Json;
using FluentAssertions;
using Microsoft.Extensions.Options;
using NSubstitute;
using Vole_Papillon_Damour.Application.Common.Interfaces.Services;
using Vole_Papillon_Damour.Domain.BookAggregate.ValueObjects;
using Vole_Papillon_Damour.Domain.IsbndbAggregate;
using Vole_Papillon_Damour.Infrastructure.Services.Isbndb;

namespace Vole_Papillon_Damour.Infrastructure.tests.Isbndb;

public sealed class IsbndbClientTests
{
    private static readonly DateTime Now = new(2026, 10, 23, 12, 0, 0, DateTimeKind.Utc);
    private const string ApiKey = "test-secret-value";

    [Fact]
    public async Task GetBookAsync_WithKnownIsbn_SendsAuthorizationAndRemovesImageOriginalFromRawJson()
    {
        var isbn = ParseIsbn13("9782070409228");
        HttpRequestMessage? capturedRequest = null;
        var quotaGuard = CreateQuotaGuard();
        using var httpClient = CreateHttpClient(request =>
        {
            capturedRequest = request;
            return JsonResponse(HttpStatusCode.OK, KnownBookJson);
        });
        using var limiter = CreateUnlimitedLimiter();
        var client = CreateClient(httpClient, quotaGuard, limiter, Options(ApiKey));

        var result = await client.GetBookAsync(isbn, IsbndbUsage.Scan, CancellationToken.None);

        var found = result.Should().BeOfType<IsbndbLookup.Found>().Subject;
        found.Book.Title.Should().Be("Les Misérables");
        found.Book.Image.Should().Be("https://images.isbndb.com/covers/example.jpg");
        found.Book.RawJson.Should().NotContain("image_original");
        capturedRequest.Should().NotBeNull();
        capturedRequest!.RequestUri!.AbsolutePath.Should().Be("/book/9782070409228");
        capturedRequest.RequestUri.Query.Should().BeEmpty();
        capturedRequest.Headers.Authorization!.Scheme.Should().Be(ApiKey);
        await quotaGuard.Received(1).RecordUsageAsync(IsbndbUsage.Scan, 1, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GetBookAsync_WhenIsbnIsNotFound_DoesNotCountUsage()
    {
        var quotaGuard = CreateQuotaGuard();
        using var httpClient = CreateHttpClient(_ => JsonResponse(
            HttpStatusCode.NotFound,
            """{"message":"Book not found"}"""));
        using var limiter = CreateUnlimitedLimiter();
        var client = CreateClient(httpClient, quotaGuard, limiter, Options(ApiKey));

        var result = await client.GetBookAsync(
            ParseIsbn13("9782070363735"),
            IsbndbUsage.CatchUp,
            CancellationToken.None);

        result.Should().BeOfType<IsbndbLookup.NotFound>();
        await quotaGuard.DidNotReceive().RecordUsageAsync(
            Arg.Any<IsbndbUsage>(),
            Arg.Any<int>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GetBooksAsync_SplitsAtBulkSizeAndReturnsUnlistedIsbnsAsMissing()
    {
        var requestedBatches = new List<string[]>();
        var quotaGuard = CreateQuotaGuard();
        using var httpClient = CreateAsyncHttpClient(async request =>
        {
            var body = await request.Content!.ReadAsStringAsync();
            var isbns = JsonDocument.Parse(body).RootElement.GetProperty("isbns")
                .EnumerateArray().Select(item => item.GetString()!).ToArray();
            requestedBatches.Add(isbns);
            var found = requestedBatches.Count == 1 ? isbns : [];
            return JsonResponse(
                HttpStatusCode.OK,
                JsonSerializer.Serialize(new
                {
                    total = found.Length,
                    requested = isbns.Length,
                    data = found.Select(value => new { isbn13 = value, title = "Found book" })
                }));
        });
        using var limiter = CreateUnlimitedLimiter();
        var client = CreateClient(
            httpClient,
            quotaGuard,
            limiter,
            Options(ApiKey) with { BulkSize = 2 });
        var isbns = new[]
        {
            ParseIsbn13("9782070363735"),
            ParseIsbn13("9782070612758"),
            ParseIsbn13("9780306406157")
        };

        var result = await client.GetBooksAsync(isbns, IsbndbUsage.CatchUp, CancellationToken.None);

        result.Status.Should().Be(IsbndbBulkLookupStatus.Success);
        result.Found.Keys.Should().BeEquivalentTo(isbns.Take(2));
        result.Missing.Should().Equal(isbns[2]);
        requestedBatches.Select(batch => batch.Length).Should().Equal(2, 1);
        await quotaGuard.Received(1).RecordUsageAsync(
            IsbndbUsage.CatchUp,
            2,
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GetBookAsync_WhenQuotaIsExhausted_DoesNotSendHttpRequest()
    {
        var quotaGuard = CreateQuotaGuard(isExhausted: true);
        var requestCount = 0;
        using var httpClient = CreateHttpClient((Func<HttpRequestMessage, HttpResponseMessage>)(_ =>
        {
            requestCount++;
            throw new InvalidOperationException("HTTP should not be called when the quota is exhausted.");
        }));
        using var limiter = CreateUnlimitedLimiter();
        var client = CreateClient(httpClient, quotaGuard, limiter, Options(ApiKey));

        var result = await client.GetBookAsync(
            ParseIsbn13("9782070363735"),
            IsbndbUsage.Scan,
            CancellationToken.None);

        result.Should().BeOfType<IsbndbLookup.QuotaExhausted>();
        requestCount.Should().Be(0);
    }

    [Fact]
    public async Task GetBookAsync_WithoutApiKey_ReturnsUnavailableWithoutSendingHttpRequest()
    {
        var quotaGuard = CreateQuotaGuard();
        var requestCount = 0;
        using var httpClient = CreateHttpClient((Func<HttpRequestMessage, HttpResponseMessage>)(_ =>
        {
            requestCount++;
            throw new InvalidOperationException("HTTP should not be called without an API key.");
        }));
        using var limiter = CreateUnlimitedLimiter();
        var client = CreateClient(httpClient, quotaGuard, limiter, Options(""));

        var result = await client.GetBookAsync(
            ParseIsbn13("9782070363735"),
            IsbndbUsage.Scan,
            CancellationToken.None);

        result.Should().BeOfType<IsbndbLookup.Unavailable>()
            .Which.Reason.Should().Be("ISBNdb non configuré");
        requestCount.Should().Be(0);
    }

    [Fact]
    public async Task GetBookAsync_When429IsRepeated_UsesIncreasingBackoffAndStopsAfterConfiguredRetries()
    {
        var quotaGuard = CreateQuotaGuard();
        var responseCount = 0;
        using var httpClient = CreateHttpClient(_ =>
        {
            responseCount++;
            return JsonResponse(HttpStatusCode.TooManyRequests, "{}");
        });
        using var limiter = CreateUnlimitedLimiter();
        var timeProvider = new ImmediateDelayTimeProvider(Now);
        var client = CreateClient(httpClient, quotaGuard, limiter, Options(ApiKey), timeProvider);

        var result = await client.GetBookAsync(
            ParseIsbn13("9782070363735"),
            IsbndbUsage.Scan,
            CancellationToken.None);

        result.Should().BeOfType<IsbndbLookup.Unavailable>();
        responseCount.Should().Be(4);
        timeProvider.Delays.Should().Equal(
            TimeSpan.FromSeconds(1),
            TimeSpan.FromSeconds(2),
            TimeSpan.FromSeconds(4));
        await quotaGuard.Received(1).RecordFailureAsync(Arg.Any<string>(), Now, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GetBookAsync_WhenQuotaHeadersArePresent_ObservesTheDailyQuota()
    {
        var quotaGuard = CreateQuotaGuard();
        using var httpClient = CreateHttpClient(_ =>
        {
            var response = JsonResponse(HttpStatusCode.OK, """{"isbn13":"9782070363735"}""");
            response.Headers.TryAddWithoutValidation("ratelimit", "\"daily\";r=4997;t=15025");
            response.Headers.TryAddWithoutValidation("ratelimit-policy", "\"daily\";q=5000;w=86400");
            return response;
        });
        using var limiter = CreateUnlimitedLimiter();
        var client = CreateClient(httpClient, quotaGuard, limiter, Options(ApiKey));

        await client.GetBookAsync(
            ParseIsbn13("9782070363735"),
            IsbndbUsage.Scan,
            CancellationToken.None);

        await quotaGuard.Received(1).ObserveAsync(
            5_000,
            4_997,
            Now.AddSeconds(15_025),
            Now,
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GetBookAsync_WhenServiceFails_DoesNotWriteApiKeyToLogs()
    {
        var quotaGuard = CreateQuotaGuard();
        var logger = new CapturingLogger<IsbndbClient>();
        HttpRequestMessage? capturedRequest = null;
        using var httpClient = CreateHttpClient(request =>
        {
            capturedRequest = request;
            return JsonResponse(HttpStatusCode.InternalServerError, "service failure");
        });
        using var limiter = CreateUnlimitedLimiter();
        var client = CreateClient(httpClient, quotaGuard, limiter, Options(ApiKey), logger: logger);

        var result = await client.GetBookAsync(
            ParseIsbn13("9782070363735"),
            IsbndbUsage.Scan,
            CancellationToken.None);

        result.Should().BeOfType<IsbndbLookup.Unavailable>();
        capturedRequest!.RequestUri!.ToString().Should().NotContain(ApiKey);
        capturedRequest.Headers.Authorization!.ToString().Should().Be(ApiKey);
        logger.Messages.Should().NotContain(message => message.Contains(ApiKey, StringComparison.Ordinal));
        await quotaGuard.Received(1).RecordFailureAsync(Arg.Any<string>(), Now, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SearchAsync_UsesOneFrenchPageOfOneHundredAndCountsOneCredit()
    {
        var quotaGuard = CreateQuotaGuard();
        HttpRequestMessage? capturedRequest = null;
        using var httpClient = CreateHttpClient(request =>
        {
            capturedRequest = request;
            return JsonResponse(HttpStatusCode.OK, """
                {"total":120,"data":[{"isbn13":"9782070363735","title":"Le Petit Prince"}]}
                """);
        });
        using var limiter = CreateUnlimitedLimiter();
        var client = CreateClient(httpClient, quotaGuard, limiter, Options(ApiKey));

        var result = await client.SearchAsync("Les Misérables", IsbndbUsage.PublicSearch, CancellationToken.None);

        result.Status.Should().Be(IsbndbSearchStatus.Success);
        result.Total.Should().Be(120);
        result.Books.Should().ContainSingle();
        capturedRequest!.Method.Should().Be(HttpMethod.Get);
        capturedRequest.RequestUri!.AbsolutePath.Should().Be("/books/Les%20Mis%C3%A9rables");
        capturedRequest.RequestUri.Query.Should().Be("?page=1&pageSize=100&language=fr");
        await quotaGuard.Received(1).RecordUsageAsync(
            IsbndbUsage.PublicSearch,
            1,
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GetBookAsync_When403BodyReportsQuotaExhaustion_DoesNotRetry()
    {
        var quotaGuard = CreateQuotaGuard();
        var responseCount = 0;
        using var httpClient = CreateHttpClient(_ =>
        {
            responseCount++;
            var response = JsonResponse(HttpStatusCode.Forbidden, "daily quota limit reached");
            response.Headers.TryAddWithoutValidation("ratelimit", "\"daily\";r=0;t=3600");
            response.Headers.TryAddWithoutValidation("ratelimit-policy", "\"daily\";q=5000;w=86400");
            return response;
        });
        using var limiter = CreateUnlimitedLimiter();
        var client = CreateClient(httpClient, quotaGuard, limiter, Options(ApiKey));

        var result = await client.GetBookAsync(
            ParseIsbn13("9782070363735"),
            IsbndbUsage.Scan,
            CancellationToken.None);

        result.Should().BeOfType<IsbndbLookup.QuotaExhausted>()
            .Which.ResetsAt.Should().Be(Now.AddSeconds(3_600));
        responseCount.Should().Be(1);
        await quotaGuard.Received(1).MarkExhaustedAsync(
            Now.AddSeconds(3_600),
            Now,
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GetBookAsync_WhenCallerCancelsTheRequest_PropagatesCancellationWithoutRetry()
    {
        var quotaGuard = CreateQuotaGuard();
        var requestStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var responseCount = 0;
        using var httpClient = CreateCancellableHttpClient(async (_, cancellationToken) =>
        {
            responseCount++;
            requestStarted.TrySetResult();
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            return JsonResponse(HttpStatusCode.OK, "{}");
        });
        using var limiter = CreateUnlimitedLimiter();
        var client = CreateClient(httpClient, quotaGuard, limiter, Options(ApiKey));
        using var cancellation = new CancellationTokenSource();

        var request = client.GetBookAsync(
            ParseIsbn13("9782070363735"),
            IsbndbUsage.Scan,
            cancellation.Token);
        await requestStarted.Task;
        cancellation.Cancel();

        Func<Task> act = () => request;
        await act.Should().ThrowAsync<OperationCanceledException>();
        responseCount.Should().Be(1);
        await quotaGuard.DidNotReceive().RecordFailureAsync(
            Arg.Any<string>(),
            Arg.Any<DateTime>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GetBookAsync_ThrottlesThreeSequentialRequestsToOnePerSecond()
    {
        var quotaGuard = CreateQuotaGuard();
        using var httpClient = CreateHttpClient(_ => JsonResponse(HttpStatusCode.NotFound, "{}"));
        using var limiter = new System.Threading.RateLimiting.TokenBucketRateLimiter(
            new System.Threading.RateLimiting.TokenBucketRateLimiterOptions
            {
                TokenLimit = 1,
                TokensPerPeriod = 1,
                ReplenishmentPeriod = TimeSpan.FromSeconds(1),
                AutoReplenishment = true,
                QueueLimit = 100,
                QueueProcessingOrder = System.Threading.RateLimiting.QueueProcessingOrder.OldestFirst
            });
        var client = CreateClient(httpClient, quotaGuard, limiter, Options(ApiKey));
        var stopwatch = Stopwatch.StartNew();

        foreach (var isbn in new[] { "9782070363735", "9782070612758", "9780306406157" })
        {
            await client.GetBookAsync(ParseIsbn13(isbn), IsbndbUsage.Scan, CancellationToken.None);
        }

        stopwatch.Elapsed.Should().BeGreaterThanOrEqualTo(TimeSpan.FromSeconds(2));
    }

    private static IIsbndbQuotaGuard CreateQuotaGuard(bool isExhausted = false)
    {
        var guard = Substitute.For<IIsbndbQuotaGuard>();
        guard.IsExhaustedAsync(Arg.Any<DateTime>(), Arg.Any<CancellationToken>())
            .Returns(isExhausted);
        return guard;
    }

    private static IsbndbOptions Options(string apiKey) => new()
    {
        ApiKey = apiKey,
        BaseUrl = "https://api.example.test",
        BulkSize = 100,
        MaxRetries = 3,
        RequestsPerSecond = 1
    };

    private static IsbndbClient CreateClient(
        HttpClient httpClient,
        IIsbndbQuotaGuard quotaGuard,
        System.Threading.RateLimiting.RateLimiter limiter,
        IsbndbOptions options,
        TimeProvider? timeProvider = null,
        CapturingLogger<IsbndbClient>? logger = null) =>
        new(
            httpClient,
            Microsoft.Extensions.Options.Options.Create(options),
            quotaGuard,
            limiter,
            timeProvider ?? new FixedTimeProvider(Now),
            logger ?? new CapturingLogger<IsbndbClient>());

    private static HttpClient CreateHttpClient(
        Func<HttpRequestMessage, HttpResponseMessage> responseFactory) =>
        new(new StubHttpMessageHandler(responseFactory))
        {
            BaseAddress = new Uri("https://api.example.test")
        };

    private static HttpClient CreateAsyncHttpClient(
        Func<HttpRequestMessage, Task<HttpResponseMessage>> responseFactory) =>
        new(new StubHttpMessageHandler(responseFactory))
        {
            BaseAddress = new Uri("https://api.example.test")
        };

    private static HttpClient CreateCancellableHttpClient(
        Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> responseFactory) =>
        new(new StubHttpMessageHandler(responseFactory))
        {
            BaseAddress = new Uri("https://api.example.test")
        };

    private static System.Threading.RateLimiting.RateLimiter CreateUnlimitedLimiter() =>
        new System.Threading.RateLimiting.ConcurrencyLimiter(
            new System.Threading.RateLimiting.ConcurrencyLimiterOptions
            {
                PermitLimit = 100,
                QueueLimit = 100,
                QueueProcessingOrder = System.Threading.RateLimiting.QueueProcessingOrder.OldestFirst
            });

    private static HttpResponseMessage JsonResponse(HttpStatusCode statusCode, string json) => new(statusCode)
    {
        Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json")
    };

    private static Isbn13 ParseIsbn13(string value) =>
        Isbn13.TryCreate(value, out var isbn13) ? isbn13 : throw new InvalidOperationException(value);

    private const string KnownBookJson = """
        {
          "title": "Les Misérables",
          "title_long": "Les Misérables, tome 1",
          "isbn": "2070409226",
          "isbn13": "9782070409228",
          "isbn10": "2070409226",
          "binding": "Paperback",
          "publisher": "Gallimard",
          "language": "fre",
          "date_published": "1995",
          "edition": "Folio classique",
          "edition_number": 1,
          "pages": 512,
          "dimensions": "18 x 11 cm",
          "dimensions_structured": { "height": { "value": 18, "unit": "cm" } },
          "image": "https://images.isbndb.com/covers/example.jpg",
          "image_original": "https://images.isbndb.com/private?token=do-not-store",
          "msrp": 12.5,
          "list_price": { "amount": 10.5, "currency": "EUR" },
          "synopsis": "Résumé du livre.",
          "overview": "Autre résumé.",
          "excerpt": "Un extrait.",
          "dewey_decimal": "843.7",
          "authors": ["Hugo, Victor"],
          "subjects": ["Romans", "Literature & Fiction"],
          "other_isbns": ["9782070612758"],
          "related": { "work": "les-miserables" }
        }
        """;

    private sealed class StubHttpMessageHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> _responseFactory;

        public StubHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> responseFactory)
            : this((request, _) => Task.FromResult(responseFactory(request)))
        {
        }

        public StubHttpMessageHandler(Func<HttpRequestMessage, Task<HttpResponseMessage>> responseFactory)
            : this((request, _) => responseFactory(request))
        {
        }

        public StubHttpMessageHandler(
            Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> responseFactory)
        {
            _responseFactory = responseFactory;
        }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken) => _responseFactory(request, cancellationToken);
    }

    private sealed class FixedTimeProvider(DateTime now) : TimeProvider
    {
        private readonly DateTimeOffset _now = new(DateTime.SpecifyKind(now, DateTimeKind.Utc));
        public override DateTimeOffset GetUtcNow() => _now;
    }

    private sealed class ImmediateDelayTimeProvider(DateTime now) : TimeProvider
    {
        private readonly DateTimeOffset _now = new(DateTime.SpecifyKind(now, DateTimeKind.Utc));
        public List<TimeSpan> Delays { get; } = [];

        public override DateTimeOffset GetUtcNow() => _now;

        public override ITimer CreateTimer(
            TimerCallback callback,
            object? state,
            TimeSpan dueTime,
            TimeSpan period)
        {
            Delays.Add(dueTime);
            return new Timer(callback, state, TimeSpan.FromMilliseconds(1), Timeout.InfiniteTimeSpan);
        }
    }

    private sealed class CapturingLogger<T> : Microsoft.Extensions.Logging.ILogger<T>
    {
        public List<string> Messages { get; } = [];

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(Microsoft.Extensions.Logging.LogLevel logLevel) => true;

        public void Log<TState>(
            Microsoft.Extensions.Logging.LogLevel logLevel,
            Microsoft.Extensions.Logging.EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter) => Messages.Add(formatter(state, exception));
    }
}
