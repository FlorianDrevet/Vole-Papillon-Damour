using MediatR;
using Vole_Papillon_Damour.Application.Books.Common;
using Vole_Papillon_Damour.Domain.BookAggregate.ValueObjects;

namespace Vole_Papillon_Damour.Application.Books.Commands.Background;

public sealed record EnrichPendingBooksCommand(int BatchSize = 50, Isbn13? Isbn13 = null)
    : IRequest<EnrichPendingBooksResult>;
