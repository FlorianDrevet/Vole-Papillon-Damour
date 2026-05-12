using ErrorOr;
using MapsterMapper;
using MediatR;
using Vole_Papillon_Damour.Application.Actuality.Common;
using Vole_Papillon_Damour.Application.Common.Interfaces.Persistence;

namespace Vole_Papillon_Damour.Application.Actuality.Queries.GetAllActuality;

public class GetAllActualityQueryHandler(IActualityRepository actualityRepository, IMapper mapper)
    : IRequestHandler<GetAllActualityQuery, ErrorOr<List<ActualityResult>>>
{
    public async Task<ErrorOr<List<ActualityResult>>> Handle(GetAllActualityQuery command, CancellationToken cancellationToken)
    {
        var actualities = await actualityRepository.GetAllAsync();
        actualities = actualities
            .OrderByDescending(a => a.Date.Year)
            .ThenByDescending(a => a.Date.Month)
            .ThenBy(a => a.Date.Day);
        return mapper.Map<List<ActualityResult>>(actualities);
    }
}