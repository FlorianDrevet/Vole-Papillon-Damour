using System.Diagnostics;

namespace Vole_Papillon_Damour.Application.Common.Observability;

/// <summary>
/// Defines low-cardinality traces for book recommendation embedding calls.
/// </summary>
public static class BookRecommendationTelemetry
{
    /// <summary>The activity source name shared by the API and Worker.</summary>
    public const string ActivitySourceName = "Vpd.Recommendations";

    /// <summary>The activity name for one embedding request batch.</summary>
    public const string EmbeddingGenerationActivityName = "books.recommendations.embedding.generate";

    /// <summary>The number of text inputs in an embedding request.</summary>
    public const string EmbeddingInputCountTagName = "embedding.input_count";

    /// <summary>The embedding vector dimensions requested from the provider.</summary>
    public const string EmbeddingDimensionsTagName = "embedding.dimensions";

    /// <summary>The outcome of an embedding request.</summary>
    public const string EmbeddingOutcomeTagName = "embedding.outcome";

    /// <summary>The number of input tokens reported by the provider.</summary>
    public const string EmbeddingInputTokensTagName = "gen_ai.usage.input_tokens";

    private static readonly ActivitySource ActivitySource = new(ActivitySourceName);

    /// <summary>Starts a span for one embedding request without recording input text or ISBNs.</summary>
    public static Activity? StartEmbeddingGeneration(int inputCount, int dimensions, string? modelName)
    {
        var activity = ActivitySource.StartActivity(EmbeddingGenerationActivityName, ActivityKind.Client);
        activity?.SetTag("gen_ai.operation.name", "embeddings");
        activity?.SetTag(EmbeddingInputCountTagName, inputCount);
        activity?.SetTag(EmbeddingDimensionsTagName, dimensions);

        if (!string.IsNullOrWhiteSpace(modelName))
        {
            activity?.SetTag("gen_ai.request.model", modelName);
        }

        return activity;
    }

    /// <summary>Annotates a completed embedding request with usage and outcome data.</summary>
    public static void RecordEmbeddingOutcome(Activity? activity, string outcome, long? inputTokens)
    {
        if (activity is null)
        {
            return;
        }

        activity.SetTag(EmbeddingOutcomeTagName, outcome);

        if (inputTokens is >= 0)
        {
            activity.SetTag(EmbeddingInputTokensTagName, inputTokens.Value);
        }

        if (outcome != "success")
        {
            activity.SetStatus(ActivityStatusCode.Error, outcome);
        }
    }
}
