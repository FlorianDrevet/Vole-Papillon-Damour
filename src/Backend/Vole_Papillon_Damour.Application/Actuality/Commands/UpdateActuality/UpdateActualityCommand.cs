using ErrorOr;
using MediatR;
using Microsoft.AspNetCore.Http;
using Vole_Papillon_Damour.Application.Actuality.Common;
using Vole_Papillon_Damour.Domain.ActualityAggregate.ValueObjects;

namespace Vole_Papillon_Damour.Application.Actuality.Commands.UpdateActuality;

public record UpdateActualityCommand(
    ActualityId Id,
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