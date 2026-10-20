using ErrorOr;
using MediatR;
using Vole_Papillon_Damour.Application.Books.Common;
using Vole_Papillon_Damour.Domain.UserAggregate.ValueObjects;

namespace Vole_Papillon_Damour.Application.Books.Commands.UploadBookCover;

public sealed record UploadBookCoverCommand(
    string Isbn13,
    Stream Stream,
    string ContentType,
    long SizeBytes,
    UserId UpdatedBy) : IRequest<ErrorOr<UploadBookCoverResult>>;
