using MediatR;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Vole_Papillon_Damour.Application.Actuality.Commands.Background;
using Vole_Papillon_Damour.Infrastructure.Services.Social;

namespace Vole_Papillon_Damour.Worker;

public sealed class SocialImportFunction(
    IServiceScopeFactory scopeFactory,
    ILogger<SocialImportFunction> logger,
    IOptions<InstagramOptions> socialOptions)
{
    private readonly InstagramOptions _socialOptions = socialOptions.Value;

    [Function("ImportSocialActualities")]
    public async Task Run(
        [TimerTrigger("%SocialImport:Schedule%")] TimerInfo timer,
        CancellationToken cancellationToken)
    {
        if (_socialOptions.IsAccessTokenNearExpiry(DateTimeOffset.UtcNow))
        {
            logger.LogWarning(
                "Social actuality access token is near expiry; refresh it before the 60-day lifetime ends.");
        }

        using var scope = scopeFactory.CreateScope();
        var sender = scope.ServiceProvider.GetRequiredService<ISender>();

        try
        {
            var result = await sender.Send(
                new ImportSocialActualitiesCommand(),
                cancellationToken);

            logger.LogInformation(
                "Social actuality import completed. Examined: {Examined}, Imported: {Imported}, " +
                "AlreadyKnown: {AlreadyKnown}, Failed: {Failed}, NoMedia: {NoMedia}, " +
                "FallbackTitles: {FallbackTitles}",
                result.ExaminedCount,
                result.ImportedCount,
                result.AlreadyKnownCount,
                result.FailedCount,
                result.NoMediaCount,
                result.FallbackTitleCount);
        }
        catch (SocialFeedAuthenticationException exception)
        {
            logger.LogError(
                exception,
                "Social actuality import authentication failed; the access token needs attention.");
            throw;
        }
        catch (SocialFeedQuotaException exception)
        {
            logger.LogWarning(
                exception,
                "Social actuality import was throttled by the provider.");
            throw;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Social actuality import failed unexpectedly.");
            throw;
        }
    }
}
