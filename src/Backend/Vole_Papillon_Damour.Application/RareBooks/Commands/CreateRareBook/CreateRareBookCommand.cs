using ErrorOr;
using MediatR;
using Vole_Papillon_Damour.Application.RareBooks.Common;
using Vole_Papillon_Damour.Domain.UserAggregate.ValueObjects;

namespace Vole_Papillon_Damour.Application.RareBooks.Commands.CreateRareBook;

public sealed record CreateRareBookCommand(
    string Title,
    string? AuthorMention,
    string? Publisher,
    int? PublicationYear,
    decimal Price,
    string Condition,
    string? PublicDescription,
    string? Isbn13,
    UserId UserId,
    Guid? ClientGestureId = null) : IRequest<ErrorOr<RareBookResult>>;
