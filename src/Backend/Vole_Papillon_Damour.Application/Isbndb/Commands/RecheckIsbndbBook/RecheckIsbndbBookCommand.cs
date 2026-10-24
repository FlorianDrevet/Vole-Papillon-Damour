using ErrorOr;
using MediatR;

namespace Vole_Papillon_Damour.Application.Isbndb.Commands.RecheckIsbndbBook;

public sealed record RecheckIsbndbBookCommand(string Isbn13) : IRequest<ErrorOr<RecheckIsbndbBookResult>>;

public sealed record RecheckIsbndbBookResult(string Isbn13, string Outcome, DateTimeOffset? ResetsAt = null);
