using ErrorOr;
using MapsterMapper;
using MediatR;
using Vole_Papillon_Damour.Application.Actuality.Common;
using Vole_Papillon_Damour.Application.Common.Interfaces.Persistence;

namespace Vole_Papillon_Damour.Application.Actuality.Queries;

public class GetLatestActualityQueryHandler(IActualityRepository actualityRepository, IMapper mapper)
    : IRequestHandler<GetLatestActualityQuery, ErrorOr<List<ActualityResult>>>
{
    public async Task<ErrorOr<List<ActualityResult>>> Handle(GetLatestActualityQuery command, CancellationToken cancellationToken)
    {
        var actualities = await actualityRepository.GetLatestActualityAsync();
        return mapper.Map<List<ActualityResult>>(actualities);
    }
}