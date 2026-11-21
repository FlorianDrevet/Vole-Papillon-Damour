using ErrorOr;
using MediatR;
using Vole_Papillon_Damour.Domain.UserAggregate.ValueObjects;

namespace Vole_Papillon_Damour.Application.Books.Commands.BookPhotos.DeleteBookPhoto;

public sealed record DeleteBookPhotoCommand(string Code, Guid PhotoId, UserId UserId) : IRequest<ErrorOr<Deleted>>;