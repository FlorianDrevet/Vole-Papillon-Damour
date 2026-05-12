using Azure.AI.Vision.ImageAnalysis;
using Microsoft.Extensions.Logging;
using Vole_Papillon_Damour.Application.Common.Interfaces.Services;

namespace Vole_Papillon_Damour.Infrastructure.Services.OcrService;

/// <summary>
/// Prevents OCR-dependent features from crashing local startup when OCR secrets are not configured.
/// </summary>
public sealed class DisabledOcrService : IOcrService
{
    private readonly ILogger<DisabledOcrService> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="DisabledOcrService"/> class.
    /// </summary>
    /// <param name="logger">Logs the local fallback usage.</param>
    public DisabledOcrService(ILogger<DisabledOcrService> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Fails lazily when OCR is invoked without the required local secrets.
    /// </summary>
    /// <param name="stream">Provides the image stream to analyze.</param>
    /// <param name="cancellationToken">Propagates the cancellation signal.</param>
    /// <returns>Never returns successfully because OCR is disabled.</returns>
    /// <exception cref="InvalidOperationException">Thrown when OCR is requested without valid local configuration.</exception>
    public Task<ImageAnalysisResult> ExtractTextFromImage(Stream stream, CancellationToken cancellationToken)
    {
        const string message = "OCR is disabled for local development. Configure OcrSettings__VisionEndpoint and OcrSettings__VisionKey to enable image analysis.";

        _logger.LogWarning(message);
        throw new InvalidOperationException(message);
    }
}