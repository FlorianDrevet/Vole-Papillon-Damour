using ErrorOr;
using MediatR;
using Vole_Papillon_Damour.Application.Actuality.Common;
using Vole_Papillon_Damour.Domain.ActualityAggregate.ValueObjects;

namespace Vole_Papillon_Damour.Application.Actuality.Commands.PublishActuality;

public sealed record PublishActualityCommand(
    ActualityId Id) : IRequest<ErrorOr<ActualityResult>>;
