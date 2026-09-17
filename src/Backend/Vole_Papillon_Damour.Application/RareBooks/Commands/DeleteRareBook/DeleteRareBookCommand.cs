using ErrorOr;
using MediatR;
using Vole_Papillon_Damour.Domain.RareBookAggregate.ValueObjects;
using Vole_Papillon_Damour.Domain.UserAggregate.ValueObjects;

namespace Vole_Papillon_Damour.Application.RareBooks.Commands.DeleteRareBook;

public sealed record DeleteRareBookCommand(
    RareBookId RareBookId,
    UserId UserId) : IRequest<ErrorOr<bool>>;
