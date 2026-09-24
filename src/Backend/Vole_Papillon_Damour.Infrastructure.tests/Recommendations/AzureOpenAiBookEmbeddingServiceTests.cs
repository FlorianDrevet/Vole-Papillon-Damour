using FluentAssertions;
using Microsoft.Extensions.AI;
using NSubstitute;
using Vole_Papillon_Damour.Infrastructure.Services.Recommendations;

namespace Vole_Papillon_Damour.Infrastructure.tests.Recommendations;

public sealed class AzureOpenAiBookEmbeddingServiceTests
{
    [Fact]
    public async Task EmbedAsync_Requests512Dimensions()
    {
        var dimensions = new List<int?>();
        var generator = CreateGenerator(_ => Vector(3f, 4f), options => dimensions.Add(options.Dimensions));
        var service = new AzureOpenAiBookEmbeddingService(generator);

        await service.EmbedAsync(["notice"], CancellationToken.None);

        dimensions.Should().Equal(512);
    }

    [Fact]
    public async Task EmbedAsync_SplitsInputsIntoBatchesOfAtMost100()
    {
        var batchSizes = new List<int>();
        var generator = CreateGenerator(
            _ => Vector(1f, 0f),
            onBatch: inputs => batchSizes.Add(inputs));
        var service = new AzureOpenAiBookEmbeddingService(generator);

        await service.EmbedAsync(Enumerable.Range(0, 205).Select(i => $"notice {i}").ToArray(), CancellationToken.None);

        batchSizes.Should().Equal(100, 100, 5);
    }

    [Fact]
    public async Task EmbedAsync_NormalizesEachReturnedVector()
    {
        var service = new AzureOpenAiBookEmbeddingService(CreateGenerator(_ => Vector(3f, 4f)));

        var vectors = await service.EmbedAsync(["notice"], CancellationToken.None);

        vectors[0].Should().HaveCount(512);
        MathF.Sqrt(vectors[0].Sum(value => value * value)).Should().BeApproximately(1f, 0.00001f);
        vectors[0].Take(2).Should().Equal(0.6f, 0.8f);
    }

    [Fact]
    public async Task EmbedAsync_RejectsVectorsWithUnexpectedDimension()
    {
        var service = new AzureOpenAiBookEmbeddingService(CreateGenerator(_ => new float[1536]));

        Func<Task> act = async () =>
        {
            await service.EmbedAsync(["notice"], CancellationToken.None);
        };

        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    private static IEmbeddingGenerator<string, Embedding<float>> CreateGenerator(
        Func<string, float[]> vectorFactory,
        Action<EmbeddingGenerationOptions>? onOptions = null,
        Action<int>? onBatch = null)
    {
        var generator = Substitute.For<IEmbeddingGenerator<string, Embedding<float>>>();
        generator.GenerateAsync(
                Arg.Any<IEnumerable<string>>(),
                Arg.Any<EmbeddingGenerationOptions>(),
                Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                var inputs = call.Arg<IEnumerable<string>>().ToArray();
                onOptions?.Invoke(call.Arg<EmbeddingGenerationOptions>());
                onBatch?.Invoke(inputs.Length);
                var embeddings = inputs.Select(input => new Embedding<float>(vectorFactory(input))).ToArray();
                return Task.FromResult(new GeneratedEmbeddings<Embedding<float>>(embeddings));
            });
        return generator;
    }

    private static float[] Vector(float first, float second)
    {
        var vector = new float[512];
        vector[0] = first;
        vector[1] = second;
        return vector;
    }
}
