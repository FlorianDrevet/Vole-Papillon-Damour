namespace Vole_Papillon_Damour.Application.Common.Interfaces.Services;

public interface IActualityTitleGenerator
{
    Task<string?> GenerateAsync(string article, CancellationToken cancellationToken);
}
