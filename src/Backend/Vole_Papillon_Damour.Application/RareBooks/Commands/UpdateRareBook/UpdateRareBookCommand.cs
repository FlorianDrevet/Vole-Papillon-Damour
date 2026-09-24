using ErrorOr;
using MediatR;
using Vole_Papillon_Damour.Application.RareBooks.Common;
using Vole_Papillon_Damour.Domain.RareBookAggregate.ValueObjects;
using Vole_Papillon_Damour.Domain.UserAggregate.ValueObjects;

namespace Vole_Papillon_Damour.Application.RareBooks.Commands.UpdateRareBook;

public sealed record UpdateRareBookCommand(
    RareBookId RareBookId,
    string Title,
    string? AuthorMention,
    string? Publisher,
    int? PublicationYear,
    decimal Price,
    string Condition,
    string? PublicDescription,
    string? Isbn13,
    byte[] RowVersion,
    UserId UserId) : IRequest<ErrorOr<RareBookResult>>;
