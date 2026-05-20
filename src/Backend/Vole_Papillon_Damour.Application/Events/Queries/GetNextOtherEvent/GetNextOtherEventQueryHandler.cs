using ErrorOr;
using MapsterMapper;
using MediatR;
using Vole_Papillon_Damour.Application.Common.Interfaces.Persistence;
using Vole_Papillon_Damour.Application.Events.Common;
using Vole_Papillon_Damour.Application.Events.Queries.GetNextBooks;
using Vole_Papillon_Damour.Domain.Common.Errors;

namespace Vole_Papillon_Damour.Application.Events.Queries.GetNextOtherEvent;

public class GetNextOtherEventQueryHandler(IEventRepository eventRepository, IMapper mapper)
    : IRequestHandler<GetNextOtherEventQuery, ErrorOr<List<AssoEventResult>>>
{
    public async Task<ErrorOr<List<AssoEventResult>>> Handle(GetNextOtherEventQuery command, CancellationToken cancellationToken)
    {
        var nextOtherEvent = await eventRepository.GetNextOtherAsync();
        
        return mapper.Map<List<AssoEventResult>>(nextOtherEvent);
    }
}