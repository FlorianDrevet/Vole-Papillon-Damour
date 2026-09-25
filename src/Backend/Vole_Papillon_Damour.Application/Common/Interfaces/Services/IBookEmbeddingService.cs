namespace Vole_Papillon_Damour.Application.Common.Interfaces.Services;

public interface IBookEmbeddingService
{
    bool IsConfigured { get; }

    /// <returns>One normalized 512-float vector per text, in input order.</returns>
    Task<IReadOnlyList<float[]>> EmbedAsync(
        IReadOnlyList<string> texts,
        CancellationToken cancellationToken);
}
