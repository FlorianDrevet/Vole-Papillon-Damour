using ErrorOr;
using MediatR;
using Vole_Papillon_Damour.Application.RareBooks.Common;
using Vole_Papillon_Damour.Domain.RareBookAggregate.ValueObjects;
using Vole_Papillon_Damour.Domain.UserAggregate.ValueObjects;

namespace Vole_Papillon_Damour.Application.RareBooks.Commands.DeleteRareBookPhoto;

public sealed record DeleteRareBookPhotoCommand(
    RareBookPhotoId RareBookPhotoId,
    UserId UserId) : IRequest<ErrorOr<RareBookResult>>;
