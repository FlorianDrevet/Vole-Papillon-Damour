using ErrorOr;
using MediatR;
using Vole_Papillon_Damour.Application.RareBooks.Common;
using Vole_Papillon_Damour.Domain.RareBookAggregate.ValueObjects;
using Vole_Papillon_Damour.Domain.UserAggregate.ValueObjects;

namespace Vole_Papillon_Damour.Application.RareBooks.Commands.AddRareBookPhoto;

public sealed record AddRareBookPhotoCommand(
    RareBookId RareBookId,
    Stream Stream,
    string FileName,
    string ContentType,
    long SizeBytes,
    string? Caption,
    UserId UserId) : IRequest<ErrorOr<RareBookResult>>;
