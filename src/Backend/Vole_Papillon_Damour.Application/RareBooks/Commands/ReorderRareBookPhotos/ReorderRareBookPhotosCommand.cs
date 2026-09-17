using ErrorOr;
using MediatR;
using Vole_Papillon_Damour.Application.RareBooks.Common;
using Vole_Papillon_Damour.Domain.RareBookAggregate.ValueObjects;
using Vole_Papillon_Damour.Domain.UserAggregate.ValueObjects;

namespace Vole_Papillon_Damour.Application.RareBooks.Commands.ReorderRareBookPhotos;

public sealed record ReorderRareBookPhotosCommand(
    RareBookId RareBookId,
    IReadOnlyList<RareBookPhotoId> OrderedPhotoIds,
    UserId UserId) : IRequest<ErrorOr<RareBookResult>>;
