using ErrorOr;
using MapsterMapper;
using MediatR;
using Vole_Papillon_Damour.Application.Actuality.Common;
using Vole_Papillon_Damour.Application.Common.Interfaces.Persistence;
using Vole_Papillon_Damour.Application.Common.Interfaces.Services;

namespace Vole_Papillon_Damour.Application.Actuality.Commands.AddActuality;

public class AddACtualityCommandHandler(IActualityRepository actualityRepository, IMapper mapper,
    IBlobService blobService)
    : IRequestHandler<AddACtualityCommand, ErrorOr<ActualityResult>>
{
    public async Task<ErrorOr<ActualityResult>> Handle(AddACtualityCommand command, CancellationToken cancellationToken)
    {
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
        
        var actuality = Domain.ActualityAggregate.Actuality.Create(
            command.Title,
            command.Article,
            urlPrincipalImage!,
            command.FacebookLink,
            command.InstagramLink,
            imagesUri,
            command.Date
        );
        actuality = await actualityRepository.AddAsync(actuality);
        
        return mapper.Map<ActualityResult>(actuality);
    }
}