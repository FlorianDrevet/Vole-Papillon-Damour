using Vole_Papillon_Damour.Application.Common.Interfaces.Services;

namespace Vole_Papillon_Damour.Infrastructure.Services.Recommendations;

public sealed class NoOpBookEmbeddingService : IBookEmbeddingService
{
    public bool IsConfigured => false;

    public Task<IReadOnlyList<float[]>> EmbedAsync(
        IReadOnlyList<string> texts,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(texts);
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult<IReadOnlyList<float[]>>(Array.Empty<float[]>());
    }
}
