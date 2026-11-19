using ErrorOr;
using MediatR;
using Vole_Papillon_Damour.Application.Books.Common;
using Vole_Papillon_Damour.Domain.UserAggregate.ValueObjects;

namespace Vole_Papillon_Damour.Application.Books.Commands.BookPhotos.AddBookPhoto;

public sealed record AddBookPhotoCommand(
    string Code,
    Stream Stream,
    string FileName,
    string ContentType,
    long SizeBytes,
    UserId UserId) : IRequest<ErrorOr<BookPhotoResult>>;