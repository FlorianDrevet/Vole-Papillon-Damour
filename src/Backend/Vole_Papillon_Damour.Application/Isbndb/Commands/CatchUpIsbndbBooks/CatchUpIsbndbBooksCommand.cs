using MediatR;

namespace Vole_Papillon_Damour.Application.Isbndb.Commands.CatchUpIsbndbBooks;

public sealed record CatchUpIsbndbBooksCommand(
    int BulkSize = 100,
    int AbsentRecheckDays = 30) : IRequest<CatchUpIsbndbBooksResult>;
