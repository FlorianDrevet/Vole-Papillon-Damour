using ErrorOr;
using MediatR;
using Microsoft.Extensions.Logging;
using Vole_Papillon_Damour.Application.Common.Interfaces.Persistence;
using Vole_Papillon_Damour.Application.Common.Interfaces.Services;
using Vole_Papillon_Damour.Domain.Common.Errors;

namespace Vole_Papillon_Damour.Application.Actuality.Commands.DeleteActuality;

public class DeleteActualityCommandHandler(
    IActualityRepository actualityRepository,
    IBlobService blobService,
    ILogger<DeleteActualityCommandHandler> logger)
    : IRequestHandler<DeleteActualityCommand, ErrorOr<bool>>
{
    public async Task<ErrorOr<bool>> Handle(DeleteActualityCommand command, CancellationToken cancellationToken)
    {
        var actuality = await actualityRepository.GetByIdAsync(command.ActualityId);
        if (actuality is null)
        {
            return Errors.Actuality.ActualityNotFound(command.ActualityId);
        }

        var deleted = await actualityRepository.DeleteAsync(command.ActualityId);

        if (!deleted)
        {
            return Errors.Actuality.ActualityNotFound(command.ActualityId);
        }

        var imageUris = new[] { actuality.UrlPrincipalImage }
            .Concat(actuality.Images)
            .Distinct()
            .ToList();
        foreach (var imageUri in imageUris)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                await blobService.DeleteFileAsync(imageUri.ToString());
            }
            catch (Exception exception)
            {
                logger.LogWarning(
                    exception,
                    "Actuality image cleanup failed after deletion. ActualityId: {ActualityId}",
                    command.ActualityId.Value);
            }
        }

        return deleted;
    }
}
