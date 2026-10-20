using ErrorOr;
using MediatR;
using Vole_Papillon_Damour.Application.Books.Common;
using Vole_Papillon_Damour.Domain.UserAggregate.ValueObjects;

namespace Vole_Papillon_Damour.Application.Books.Commands.Background;

public sealed record RetryBookMetadataCommand(string Isbn, UserId RequestedBy)
    : IRequest<ErrorOr<RetryBookMetadataResult>>;
