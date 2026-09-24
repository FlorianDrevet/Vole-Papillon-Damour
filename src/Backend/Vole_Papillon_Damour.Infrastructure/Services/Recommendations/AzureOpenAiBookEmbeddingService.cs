using Microsoft.Extensions.AI;
using System.Numerics.Tensors;
using Vole_Papillon_Damour.Application.Common.Interfaces.Services;

namespace Vole_Papillon_Damour.Infrastructure.Services.Recommendations;

public sealed class AzureOpenAiBookEmbeddingService(
    IEmbeddingGenerator<string, Embedding<float>> generator) : IBookEmbeddingService
{
    private const int Dimensions = 512;
    private const int BatchSize = 100;

    public bool IsConfigured => true;

    public async Task<IReadOnlyList<float[]>> EmbedAsync(
        IReadOnlyList<string> texts,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(texts);

        var vectors = new List<float[]>(texts.Count);
        for (var offset = 0; offset < texts.Count; offset += BatchSize)
        {
            var count = Math.Min(BatchSize, texts.Count - offset);
            var batch = new string[count];
            for (var index = 0; index < count; index++)
            {
                batch[index] = texts[offset + index];
            }

            var embeddings = await generator.GenerateAsync(
                batch,
                new EmbeddingGenerationOptions { Dimensions = Dimensions },
                cancellationToken);

            if (embeddings.Count != count)
            {
                throw new InvalidOperationException(
                    $"The embedding provider returned {embeddings.Count} vectors for {count} texts.");
            }

            foreach (var embedding in embeddings)
            {
                var vector = embedding.Vector.ToArray();
                if (vector.Length != Dimensions)
                {
                    throw new InvalidOperationException(
                        $"Expected a {Dimensions}-dimensional embedding, but received {vector.Length} values.");
                }

                var norm = TensorPrimitives.Norm(vector.AsSpan());
                if (!float.IsFinite(norm) || norm <= 0f)
                {
                    throw new InvalidOperationException("The embedding provider returned a vector that cannot be normalized.");
                }

                var normalized = new float[Dimensions];
                TensorPrimitives.Divide(vector.AsSpan(), norm, normalized.AsSpan());
                vectors.Add(normalized);
            }
        }

        return vectors;
    }
}
