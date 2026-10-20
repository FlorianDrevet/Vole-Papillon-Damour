using ErrorOr;
using MediatR;
using Vole_Papillon_Damour.Application.Books.Common;

namespace Vole_Papillon_Damour.Application.Books.Commands.Admin;

public sealed record ImportAdminBooksCommand(IReadOnlyList<string>? Isbns)
    : IRequest<ErrorOr<AdminBooksImportResult>>;
