using System.Runtime.InteropServices;
using Vole_Papillon_Damour.Domain.RecommendationAggregate;

namespace Vole_Papillon_Damour.Application.Recommendations.Commands.RecomputeBookNeighbors;

public static class EmbeddingBytes
{
    public static byte[] ToBytes(float[] vector)
    {
        ArgumentNullException.ThrowIfNull(vector);
        if (vector.Length != BookSimilarityProfile.Dimensions)
        {
            throw new ArgumentException(
                $"An embedding must contain {BookSimilarityProfile.Dimensions} float32 values.",
                nameof(vector));
        }

        return MemoryMarshal.AsBytes(vector.AsSpan()).ToArray();
    }

    public static float[] ToFloats(byte[] bytes)
    {
        ArgumentNullException.ThrowIfNull(bytes);
        if (bytes.Length != BookSimilarityProfile.EmbeddingBytes)
        {
            throw new ArgumentException(
                $"An embedding must hold {BookSimilarityProfile.EmbeddingBytes} bytes.",
                nameof(bytes));
        }

        return MemoryMarshal.Cast<byte, float>(bytes.AsSpan()).ToArray();
    }
}
