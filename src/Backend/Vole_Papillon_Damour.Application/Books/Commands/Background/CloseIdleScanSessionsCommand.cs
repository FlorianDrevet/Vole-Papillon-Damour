using MediatR;
using Vole_Papillon_Damour.Application.Books.Common;

namespace Vole_Papillon_Damour.Application.Books.Commands.Background;

public sealed record CloseIdleScanSessionsCommand
    : IRequest<CloseIdleScanSessionsResult>;
