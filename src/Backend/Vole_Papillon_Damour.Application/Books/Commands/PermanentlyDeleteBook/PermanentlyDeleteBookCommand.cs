using ErrorOr;
using MediatR;
using Vole_Papillon_Damour.Application.Books.Common;
using Vole_Papillon_Damour.Domain.UserAggregate.ValueObjects;

namespace Vole_Papillon_Damour.Application.Books.Commands.PermanentlyDeleteBook;

public sealed record PermanentlyDeleteBookCommand(
    string Isbn,
    UserId DeletedBy) : IRequest<ErrorOr<DeleteBookResult>>;
