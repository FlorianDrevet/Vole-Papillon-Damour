using MediatR;

namespace Vole_Papillon_Damour.Application.Actuality.Commands.Background;

public sealed record ImportSocialActualitiesCommand
    : IRequest<ImportSocialActualitiesResult>;
