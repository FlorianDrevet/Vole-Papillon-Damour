using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Vole_Papillon_Damour.Application.Common.Interfaces.Services;

namespace Vole_Papillon_Damour.Infrastructure.Services.Ai;

public sealed class FoundryActualityTitleGenerator(
    IChatClient chatClient,
    IOptions<TitleGenerationOptions> options,
    ILogger<FoundryActualityTitleGenerator> logger) : IActualityTitleGenerator
{
    private const int MaximumArticleLength = 2_000;
    private readonly TitleGenerationOptions _options = options.Value;

    public async Task<string?> GenerateAsync(
        string article,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(article))
        {
            return null;
        }

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(_options.TimeoutMilliseconds);

        try
        {
            var promptArticle = article.Length > MaximumArticleLength
                ? article[..MaximumArticleLength]
                : article;
            var response = await chatClient.GetResponseAsync(
                [
                    new ChatMessage(
                        ChatRole.System,
                        "Tu proposes un titre court en français pour une actualité associative. " +
                        "Réponds uniquement par le titre, sans guillemets ni préfixe."),
                    new ChatMessage(ChatRole.User, promptArticle),
                ],
                new ChatOptions
                {
                    Temperature = _options.Temperature,
                    MaxOutputTokens = _options.MaxOutputTokens,
                },
                timeout.Token);

            return GeneratedTitleValidator.NormalizeAndValidate(response.Text);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            logger.LogWarning("Actuality title generation timed out.");
            return null;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception, "Actuality title generation failed.");
            return null;
        }
    }
}
