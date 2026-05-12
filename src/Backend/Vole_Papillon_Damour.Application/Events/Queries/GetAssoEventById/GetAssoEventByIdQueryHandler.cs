using ErrorOr;
using MapsterMapper;
using MediatR;
using Vole_Papillon_Damour.Application.Common.Interfaces.Persistence;
using Vole_Papillon_Damour.Application.Events.Common;
using Vole_Papillon_Damour.Domain.Common.Errors;

namespace Vole_Papillon_Damour.Application.Events.Queries.GetAssoEventById;

public class GetAssoEventByIdQueryHandler(IEventRepository eventRepository, IMapper mapper)
    : IRequestHandler<GetAssoEventByIdQuery, ErrorOr<AssoEventResult>>
{
    public async Task<ErrorOr<AssoEventResult>> Handle(GetAssoEventByIdQuery query, CancellationToken cancellationToken)
    {
        var assoEvent = await eventRepository.GetByIdAsync(query.AssoEventsId);
        
        if (assoEvent is null)
        {
            return Errors.AssoEvent.AssoEventNotFound(query.AssoEventsId);
        }
        
        return mapper.Map<AssoEventResult>(assoEvent);
    }
}