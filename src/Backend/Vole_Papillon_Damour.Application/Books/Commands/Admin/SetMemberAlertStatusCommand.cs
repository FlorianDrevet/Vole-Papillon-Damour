using ErrorOr;
using MediatR;
using Vole_Papillon_Damour.Application.Books.Common;
using Vole_Papillon_Damour.Domain.UserAggregate.ValueObjects;

namespace Vole_Papillon_Damour.Application.Books.Commands.Admin;

public sealed record SetMemberAlertStatusCommand(
    UserId MemberId,
    bool Blocked,
    UserId UpdatedBy) : IRequest<ErrorOr<AdminMemberOperationResult>>;
