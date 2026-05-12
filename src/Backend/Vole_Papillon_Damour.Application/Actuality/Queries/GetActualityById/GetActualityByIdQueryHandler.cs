using ErrorOr;
using MapsterMapper;
using MediatR;
using Vole_Papillon_Damour.Application.Actuality.Common;
using Vole_Papillon_Damour.Application.Common.Interfaces.Persistence;
using Vole_Papillon_Damour.Domain.Common.Errors;

namespace Vole_Papillon_Damour.Application.Actuality.Queries.GetActualityById;

public class GetActualityByIdQueryHandler(IActualityRepository actualityRepository, IMapper mapper)
    : IRequestHandler<GetActualityByIdQuery, ErrorOr<ActualityResult>>
{
    public async Task<ErrorOr<ActualityResult>> Handle(GetActualityByIdQuery command, CancellationToken cancellationToken)
    {
        var actuality = await actualityRepository.GetByIdAsync(command.Id);

        if (actuality is null)
        {
            return Errors.Actuality.ActualityNotFound(command.Id);
        }

        return mapper.Map<ActualityResult>(actuality);
    }
}