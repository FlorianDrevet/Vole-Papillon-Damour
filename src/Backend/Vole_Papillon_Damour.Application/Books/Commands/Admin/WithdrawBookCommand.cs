using ErrorOr;
using MediatR;
using Vole_Papillon_Damour.Application.Books.Common;
using Vole_Papillon_Damour.Domain.UserAggregate.ValueObjects;

namespace Vole_Papillon_Damour.Application.Books.Commands.Admin;

public sealed record WithdrawBookCommand(
    string Isbn,
    int Quantity,
    string Note,
    UserId UpdatedBy) : IRequest<ErrorOr<AdminBookOperationResult>>;
