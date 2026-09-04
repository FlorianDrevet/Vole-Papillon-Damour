using MediatR;
using Vole_Papillon_Damour.Application.Books.Common;

namespace Vole_Papillon_Damour.Application.Books.Commands.Background;

public sealed record EnrichPendingBooksCommand(int BatchSize = 50)
    : IRequest<EnrichPendingBooksResult>;
