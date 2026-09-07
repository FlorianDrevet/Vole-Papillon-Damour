using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace Vole_Papillon_Damour.Application.Common.Observability;

/// <summary>
/// Defines the traces and low-cardinality metrics used to diagnose book scans.
/// </summary>
public static class BookScanTelemetry
{
    /// <summary>The shared activity source name for the Books feature.</summary>
    public const string ActivitySourceName = "Vpd.Books";

    /// <summary>The shared meter name for the Books feature.</summary>
    public const string MeterName = "Vpd.Books";

    /// <summary>The activity name for the complete bibliographic resolution.</summary>
    public const string MetadataResolutionActivityName = "books.metadata.resolve";

    /// <summary>The activity name for one bibliographic provider call.</summary>
    public const string MetadataProviderActivityName = "books.metadata.provider";

    /// <summary>The activity name for the database-backed scan persistence.</summary>
    public const string ScanPersistenceActivityName = "books.scan.persist";

    /// <summary>The duration histogram for one bibliographic provider call.</summary>
    public const string MetadataProviderDurationMetricName = "vpd.books.metadata.provider.duration";

    /// <summary>The duration histogram for complete bibliographic resolution.</summary>
    public const string MetadataResolutionDurationMetricName = "vpd.books.metadata.resolution.duration";

    /// <summary>The duration histogram for database-backed scan persistence.</summary>
    public const string ScanPersistenceDurationMetricName = "vpd.books.scan.persistence.duration";

    /// <summary>The ISBN tag attached to diagnostic activities.</summary>
    public const string BookIsbnTagName = "book.isbn13";

    /// <summary>The provider tag attached to provider activities and metrics.</summary>
    public const string MetadataProviderTagName = "book.metadata.provider";

    /// <summary>The source tag attached to the complete resolution activity.</summary>
    public const string MetadataSourceTagName = "book.metadata.source";

    /// <summary>The outcome tag attached to bibliographic activities and metrics.</summary>
    public const string MetadataOutcomeTagName = "book.metadata.outcome";

    /// <summary>The client gesture identifier tag used to correlate offline scans.</summary>
    public const string ScanClientGestureIdTagName = "scan.client_gesture_id";

    /// <summary>The scan session identifier tag attached to persistence activities.</summary>
    public const string ScanSessionIdTagName = "scan.session_id";

    /// <summary>The decision tag attached to persistence activities.</summary>
    public const string ScanKeptTagName = "scan.kept";

    /// <summary>The persistence outcome tag attached to scan activities and metrics.</summary>
    public const string ScanPersistenceOutcomeTagName = "scan.persistence.outcome";

    /// <summary>The successful provider or resolution outcome.</summary>
    public const string FoundOutcome = "found";

    /// <summary>The provider or resolution outcome when no metadata was found.</summary>
    public const string NotFoundOutcome = "not_found";

    /// <summary>The outcome for an unavailable provider or unexpected operation failure.</summary>
    public const string FailedOutcome = "failed";

    /// <summary>The outcome for a provider request that exceeded its timeout.</summary>
    public const string TimeoutOutcome = "timeout";

    /// <summary>The outcome for a caller-requested cancellation.</summary>
    public const string CancelledOutcome = "cancelled";

    /// <summary>The outcome for a successfully persisted scan.</summary>
    public const string PersistedOutcome = "persisted";

    /// <summary>The outcome for an idempotent scan replay.</summary>
    public const string AlreadyProcessedOutcome = "already_processed";

    /// <summary>The outcome for a scan rejected by application validation or business rules.</summary>
    public const string RejectedOutcome = "rejected";

    private static readonly ActivitySource ActivitySource = new(ActivitySourceName);
    private static readonly Meter Meter = new(MeterName);
    private static readonly Histogram<double> MetadataProviderDuration =
        Meter.CreateHistogram<double>(
            MetadataProviderDurationMetricName,
            unit: "ms",
            description: "Duration of one bibliographic provider lookup.");
    private static readonly Histogram<double> MetadataResolutionDuration =
        Meter.CreateHistogram<double>(
            MetadataResolutionDurationMetricName,
            unit: "ms",
            description: "Duration of the complete bibliographic resolution.");
    private static readonly Histogram<double> ScanPersistenceDuration =
        Meter.CreateHistogram<double>(
            ScanPersistenceDurationMetricName,
            unit: "ms",
            description: "Duration of the database-backed scan persistence.");

    /// <summary>
    /// Starts an internal Books activity. The Azure Monitor registration decides
    /// whether it is exported; unit tests can listen to the same source.
    /// </summary>
    /// <param name="operationName">The stable operation name.</param>
    /// <returns>The activity when a listener is interested; otherwise <see langword="null"/>.</returns>
    public static Activity? StartActivity(string operationName) =>
        ActivitySource.StartActivity(operationName, ActivityKind.Internal);

    /// <summary>Records the elapsed time for a provider lookup.</summary>
    /// <param name="duration">The elapsed lookup duration.</param>
    /// <param name="provider">The low-cardinality provider name.</param>
    /// <param name="outcome">The low-cardinality lookup outcome.</param>
    public static void RecordMetadataProviderDuration(
        TimeSpan duration,
        string provider,
        string outcome)
    {
        var tags = new TagList
        {
            { MetadataProviderTagName, provider },
            { MetadataOutcomeTagName, outcome }
        };
        MetadataProviderDuration.Record(duration.TotalMilliseconds, tags);
    }

    /// <summary>Records the elapsed time for complete bibliographic resolution.</summary>
    /// <param name="duration">The elapsed resolution duration.</param>
    /// <param name="outcome">The low-cardinality resolution outcome.</param>
    /// <param name="source">The resolved metadata source, or <c>none</c>.</param>
    public static void RecordMetadataResolutionDuration(
        TimeSpan duration,
        string outcome,
        string? source)
    {
        var tags = new TagList
        {
            { MetadataOutcomeTagName, outcome },
            { MetadataSourceTagName, source ?? "none" }
        };
        MetadataResolutionDuration.Record(duration.TotalMilliseconds, tags);
    }

    /// <summary>Records the elapsed time for a database-backed scan.</summary>
    /// <param name="duration">The elapsed persistence duration.</param>
    /// <param name="outcome">The low-cardinality persistence outcome.</param>
    public static void RecordScanPersistenceDuration(TimeSpan duration, string outcome)
    {
        var tags = new TagList
        {
            { ScanPersistenceOutcomeTagName, outcome }
        };
        ScanPersistenceDuration.Record(duration.TotalMilliseconds, tags);
    }
}
