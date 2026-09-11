using ErrorOr;
using MapsterMapper;
using MediatR;
using Vole_Papillon_Damour.Application.Actuality.Common;
using Vole_Papillon_Damour.Application.Common.Interfaces.Persistence;
using Vole_Papillon_Damour.Domain.ActualityAggregate.ValueObjects;

namespace Vole_Papillon_Damour.Application.Actuality.Queries.GetDraftActualities;

public sealed class GetDraftActualitiesQueryHandler(
    IActualityRepository actualityRepository,
    IMapper mapper)
    : IRequestHandler<GetDraftActualitiesQuery, ErrorOr<List<ActualityResult>>>
{
    public async Task<ErrorOr<List<ActualityResult>>> Handle(
        GetDraftActualitiesQuery query,
        CancellationToken cancellationToken)
    {
        var actualities = await actualityRepository.GetAllAsync();
        var drafts = actualities
            .Where(actuality => actuality.Status == ActualityStatus.Draft)
            .OrderByDescending(actuality => actuality.Date)
            .ToList();

        return mapper.Map<List<ActualityResult>>(drafts);
    }
}
