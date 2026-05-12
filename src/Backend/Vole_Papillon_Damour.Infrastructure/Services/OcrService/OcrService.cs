using Azure.AI.Vision.ImageAnalysis;
using Vole_Papillon_Damour.Application.Common.Interfaces.Services;

namespace Vole_Papillon_Damour.Infrastructure.Services.OcrService;

public class OcrService: IOcrService
{
    private readonly ImageAnalysisClient _imageAnalysisClient;
    
    public OcrService(ImageAnalysisClient imageAnalysisClient)
    {
        _imageAnalysisClient = imageAnalysisClient;
    }
    
    public async Task<ImageAnalysisResult> ExtractTextFromImage(Stream stream, CancellationToken cancellationToken)
    {
        var analyseOptions = new ImageAnalysisOptions { Language = "en" };
        return await _imageAnalysisClient.AnalyzeAsync(await BinaryData.FromStreamAsync(stream, cancellationToken),
            VisualFeatures.Read, cancellationToken: cancellationToken);
    }
}