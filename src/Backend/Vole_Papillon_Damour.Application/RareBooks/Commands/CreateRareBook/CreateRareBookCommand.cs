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
    string Shelf,
    decimal Price,
    string Condition,
    string? PublicDescription,
    string? Binding,
    string? Dimensions,
    int? PageCount,
    string? ShelfLocation,
    string? PriceSetBy,
    string? Isbn13,
    UserId UserId,
    Guid? ClientGestureId = null) : IRequest<ErrorOr<RareBookResult>>;
