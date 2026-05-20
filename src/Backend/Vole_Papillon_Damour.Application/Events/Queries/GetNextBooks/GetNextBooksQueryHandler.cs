using ErrorOr;
using MapsterMapper;
using MediatR;
using Vole_Papillon_Damour.Application.Common.Interfaces.Persistence;
using Vole_Papillon_Damour.Application.Events.Common;
using Vole_Papillon_Damour.Domain.Common.Errors;

namespace Vole_Papillon_Damour.Application.Events.Queries.GetNextBooks;

public class GetNextBooksQueryHandler(IEventRepository eventRepository, IMapper mapper)
    : IRequestHandler<GetNextBooksQuery, ErrorOr<AssoEventResult>>
{
    public async Task<ErrorOr<AssoEventResult>> Handle(GetNextBooksQuery command, CancellationToken cancellationToken)
    {
        var nextBooks = await eventRepository.GetNextBooksAsync();
        if (nextBooks == null)
            return Errors.AssoEvent.AssoEventNextBooksNotFound();
        
        return mapper.Map<AssoEventResult>(nextBooks);
    }
}