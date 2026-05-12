using ErrorOr;
using MapsterMapper;
using MediatR;
using Vole_Papillon_Damour.Application.Common.Interfaces.Persistence;
using Vole_Papillon_Damour.Application.Events.Common;

namespace Vole_Papillon_Damour.Application.Events.Queries;

public class GetAllAssoEventsQueryHandler(IEventRepository eventRepository, IMapper mapper)
    : IRequestHandler<GetAllAssoEventsQuery, ErrorOr<List<AssoEventResult>>>
{
    public async Task<ErrorOr<List<AssoEventResult>>> Handle(GetAllAssoEventsQuery command, CancellationToken cancellationToken)
    {
        var assoEvents = await eventRepository.GetNextEventsAsync();
        return assoEvents.Select(mapper.Map<AssoEventResult>).ToList();
    }
}