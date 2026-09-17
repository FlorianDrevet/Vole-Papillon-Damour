using ErrorOr;
using MediatR;
using Vole_Papillon_Damour.Application.RareBooks.Common;
using Vole_Papillon_Damour.Domain.RareBookAggregate.ValueObjects;
using Vole_Papillon_Damour.Domain.UserAggregate.ValueObjects;

namespace Vole_Papillon_Damour.Application.RareBooks.Commands.PublishRareBook;

public sealed record PublishRareBookCommand(
    RareBookId RareBookId,
    UserId UserId) : IRequest<ErrorOr<RareBookPublishResult>>;
