using ErrorOr;
using MediatR;
using Vole_Papillon_Damour.Application.Events.Common;
using Vole_Papillon_Damour.Domain.EventsAggregate.ValueObjects;

namespace Vole_Papillon_Damour.Application.Events.Commands.Events.AddBingoWin;

public record AddBingoWinCommand(
    AssoEventsId AssoEventsId, bool HasBeenWon
) : IRequest<ErrorOr<AssoEventResult>>;