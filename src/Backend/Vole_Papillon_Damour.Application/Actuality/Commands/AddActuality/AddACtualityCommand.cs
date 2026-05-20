using ErrorOr;
using MediatR;
using Microsoft.AspNetCore.Http;
using Vole_Papillon_Damour.Application.Actuality.Common;

namespace Vole_Papillon_Damour.Application.Actuality.Commands.AddActuality;

public record AddACtualityCommand(
    string Title,
    string Article,
    IFormFile? PrincipalImage,
    Uri? PrincipalImageUri,
    Uri? FacebookLink, 
    Uri? InstagramLink,
    List<IFormFile> Images,
    List<Uri> ImagesUrls,
    DateTimeOffset Date
) : IRequest<ErrorOr<ActualityResult>>;