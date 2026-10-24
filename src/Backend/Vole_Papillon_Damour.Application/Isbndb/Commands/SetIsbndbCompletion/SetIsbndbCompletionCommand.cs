using ErrorOr;
using MediatR;
using Vole_Papillon_Damour.Domain.UserAggregate.ValueObjects;

namespace Vole_Papillon_Damour.Application.Isbndb.Commands.SetIsbndbCompletion;

public sealed record SetIsbndbCompletionCommand(string Isbn13, bool Completed, UserId Administrator)
    : IRequest<ErrorOr<SetIsbndbCompletionResult>>;

public sealed record SetIsbndbCompletionResult(string Isbn13, bool IsCompleted, DateTimeOffset? CompletedAt, Guid? CompletedBy, bool Changed);
