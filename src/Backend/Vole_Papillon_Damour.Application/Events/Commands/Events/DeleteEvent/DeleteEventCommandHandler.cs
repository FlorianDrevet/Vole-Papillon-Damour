using ErrorOr;
using MapsterMapper;
using MediatR;
using Vole_Papillon_Damour.Application.Common.Interfaces.Persistence;
using Vole_Papillon_Damour.Domain.Common.Errors;

namespace Vole_Papillon_Damour.Application.Events.Commands.Events.DeleteEvent;

public class DeleteEventCommandHandler(IEventRepository eventRepository)
    : IRequestHandler<DeleteEventCommand, ErrorOr<bool>>
{
    public async Task<ErrorOr<bool>> Handle(DeleteEventCommand command, CancellationToken cancellationToken)
    {
        var deleted = await eventRepository.DeleteAsync(command.EventId);

        if (!deleted)
        {
            return Errors.AssoEvent.AssoEventNotFound(command.EventId);
        }

        return deleted;
    }
}