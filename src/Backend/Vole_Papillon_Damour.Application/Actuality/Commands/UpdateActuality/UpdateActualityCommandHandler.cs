using ErrorOr;
using MapsterMapper;
using MediatR;
using Vole_Papillon_Damour.Application.Actuality.Common;
using Vole_Papillon_Damour.Application.Common.Interfaces.Persistence;
using Vole_Papillon_Damour.Application.Common.Interfaces.Services;
using Vole_Papillon_Damour.Domain.Common.Errors;

namespace Vole_Papillon_Damour.Application.Actuality.Commands.UpdateActuality;

public class UpdateActualityCommandHandler(IActualityRepository actualityRepository, IMapper mapper, IBlobService blobService)
    : IRequestHandler<UpdateActualityCommand, ErrorOr<ActualityResult>>
{
    public async Task<ErrorOr<ActualityResult>> Handle(UpdateActualityCommand command, CancellationToken cancellationToken)
    {
        var actuality = await actualityRepository.GetByIdAsync(command.Id);

        if (actuality is null)
        {
            return Errors.Actuality.ActualityNotFound(command.Id);
        }

        var urlPrincipalImage = command.PrincipalImageUri;
        if (urlPrincipalImage is null)
        {
            urlPrincipalImage = await blobService.UploadActualityImagesAsync(command.PrincipalImage!.FileName,
                command.PrincipalImage.OpenReadStream());
        }

        List<Uri> imagesUri = command.ImagesUrls ?? new();
        foreach (var image in command.Images ?? [])
        {
            var uri = await blobService.UploadActualityImagesAsync(image.FileName, image.OpenReadStream());
            imagesUri.Add(uri);
        }
        
        actuality.Update(command.Title,
            command.Article,
            urlPrincipalImage,
            command.FacebookLink,
            command.InstagramLink,
            imagesUri,
            command.Date);

        actuality = await actualityRepository.UpdateAsync(actuality);
        return mapper.Map<ActualityResult>(actuality);
    }
}