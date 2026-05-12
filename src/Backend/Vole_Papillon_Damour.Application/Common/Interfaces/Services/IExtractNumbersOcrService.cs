using Vole_Papillon_Damour.Application.Common.Models;

namespace Vole_Papillon_Damour.Application.Common.Interfaces.Services;

public interface IExtractNumbersOcrService
{
    Task<List<Models.BingoCard>> ExtractBingoCards(Stream stream, CancellationToken cancellationToken);
}