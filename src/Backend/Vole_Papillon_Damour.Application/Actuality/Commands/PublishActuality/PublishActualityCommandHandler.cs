using ErrorOr;
using MapsterMapper;
using MediatR;
using Vole_Papillon_Damour.Application.Actuality.Common;
using Vole_Papillon_Damour.Application.Common.Interfaces.Persistence;
using Vole_Papillon_Damour.Domain.Common.Errors;

namespace Vole_Papillon_Damour.Application.Actuality.Commands.PublishActuality;

public sealed class PublishActualityCommandHandler(
    IActualityRepository actualityRepository,
    IMapper mapper)
    : IRequestHandler<PublishActualityCommand, ErrorOr<ActualityResult>>
{
    public async Task<ErrorOr<ActualityResult>> Handle(
        PublishActualityCommand command,
        CancellationToken cancellationToken)
    {
        var actuality = await actualityRepository.GetByIdAsync(command.Id);
        if (actuality is null)
        {
            return Errors.Actuality.ActualityNotFound(command.Id);
        }

        if (!actuality.Publish())
        {
            return Errors.Actuality.CannotPublish(command.Id);
        }

        actuality.MarkTitleReviewed();
        actuality = await actualityRepository.UpdateAsync(actuality);
        return mapper.Map<ActualityResult>(actuality);
    }
}
