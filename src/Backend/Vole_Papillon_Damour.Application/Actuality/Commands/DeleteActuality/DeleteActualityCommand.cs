using ErrorOr;
using MediatR;
using Vole_Papillon_Damour.Domain.ActualityAggregate.ValueObjects;

namespace Vole_Papillon_Damour.Application.Actuality.Commands.DeleteActuality;

public record DeleteActualityCommand(
    ActualityId ActualityId
) : IRequest<ErrorOr<bool>>;