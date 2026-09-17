using ErrorOr;
using MediatR;
using Vole_Papillon_Damour.Application.Books.Common;
using Vole_Papillon_Damour.Domain.UserAggregate.ValueObjects;

namespace Vole_Papillon_Damour.Application.Books.Commands.BookFlags;

public sealed record HideBookCommand(
    string Isbn,
    bool Hidden,
    UserId UpdatedBy) : IRequest<ErrorOr<BookFlagResult>>;
