using Vole_Papillon_Damour.Application.Common.Interfaces.Services;

namespace Vole_Papillon_Damour.Infrastructure.Services.Social;

public sealed class NoOpActualityTitleGenerator : IActualityTitleGenerator
{
    public Task<string?> GenerateAsync(
        string article,
        CancellationToken cancellationToken)
    {
        return Task.FromResult<string?>(null);
    }
}
