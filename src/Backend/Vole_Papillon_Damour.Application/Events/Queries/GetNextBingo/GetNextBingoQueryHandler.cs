using ErrorOr;
using MapsterMapper;
using MediatR;
using Vole_Papillon_Damour.Application.Common.Interfaces.Persistence;
using Vole_Papillon_Damour.Application.Events.Common;
using Vole_Papillon_Damour.Domain.Common.Errors;

namespace Vole_Papillon_Damour.Application.Events.Queries.GetNextBingo;

public class GetNextBingoQueryHandler(IEventRepository eventRepository, IMapper mapper)
    : IRequestHandler<GetNextBingoQuery, ErrorOr<AssoEventResult>>
{
    public async Task<ErrorOr<AssoEventResult>> Handle(GetNextBingoQuery command, CancellationToken cancellationToken)
    {
        var nextBingo = await eventRepository.GetNextBingoAsync();
        if (nextBingo == null)
            return Errors.AssoEvent.AssoEventNextBingoNotFound();
        
        return mapper.Map<AssoEventResult>(nextBingo);
    }
}