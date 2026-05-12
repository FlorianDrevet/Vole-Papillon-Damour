using Azure.AI.Vision.ImageAnalysis;

namespace Vole_Papillon_Damour.Application.Common.Interfaces.Services;

public interface IOcrService
{
    Task<ImageAnalysisResult> ExtractTextFromImage(Stream stream, CancellationToken cancellationToken);
}