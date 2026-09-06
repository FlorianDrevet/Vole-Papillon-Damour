using ErrorOr;
using MediatR;
using Vole_Papillon_Damour.Application.Books.Common;
using Vole_Papillon_Damour.Domain.UserAggregate.ValueObjects;

namespace Vole_Papillon_Damour.Application.Books.Commands.Admin;

public sealed record MergeBooksCommand(
    string SourceIsbn,
    string TargetIsbn,
    string Note,
    UserId UpdatedBy) : IRequest<ErrorOr<AdminBookOperationResult>>;
