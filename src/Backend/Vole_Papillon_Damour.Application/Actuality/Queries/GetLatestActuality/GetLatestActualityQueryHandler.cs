using ErrorOr;
using MapsterMapper;
using MediatR;
using Vole_Papillon_Damour.Application.Actuality.Common;
using Vole_Papillon_Damour.Application.Common.Interfaces.Persistence;
using Vole_Papillon_Damour.Domain.ActualityAggregate.ValueObjects;

namespace Vole_Papillon_Damour.Application.Actuality.Queries;

public class GetLatestActualityQueryHandler(IActualityRepository actualityRepository, IMapper mapper)
    : IRequestHandler<GetLatestActualityQuery, ErrorOr<List<ActualityResult>>>
{
    public async Task<ErrorOr<List<ActualityResult>>> Handle(GetLatestActualityQuery command, CancellationToken cancellationToken)
    {
        var actualities = await actualityRepository.GetLatestActualityAsync();
        actualities = actualities
            .Where(actuality => actuality.Status == ActualityStatus.Published)
            .ToList();

        return mapper.Map<List<ActualityResult>>(actualities);
    }
}
