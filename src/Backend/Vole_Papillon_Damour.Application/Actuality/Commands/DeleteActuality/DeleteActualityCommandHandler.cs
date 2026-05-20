using ErrorOr;
using MapsterMapper;
using MediatR;
using Vole_Papillon_Damour.Application.Common.Interfaces.Persistence;
using Vole_Papillon_Damour.Domain.Common.Errors;

namespace Vole_Papillon_Damour.Application.Actuality.Commands.DeleteActuality;

public class DeleteActualityCommandHandler(IActualityRepository actualityRepository, IMapper mapper)
    : IRequestHandler<DeleteActualityCommand, ErrorOr<bool>>
{
    public async Task<ErrorOr<bool>> Handle(DeleteActualityCommand command, CancellationToken cancellationToken)
    {
        var deleted = await actualityRepository.DeleteAsync(command.ActualityId);

        if (!deleted)
        {
            return Errors.Actuality.ActualityNotFound(command.ActualityId);
        }

        return deleted;
    }
}